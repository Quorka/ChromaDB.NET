#![deny(clippy::all)]

use anyhow::{anyhow, Result};
use chroma_cache::FoyerCacheConfig;
use chroma_config::{registry::Registry, Configurable};
use chroma_frontend::{
    executor::config::{ExecutorConfig, LocalExecutorConfig},
    get_collection_with_segments_provider::{
        CacheInvalidationRetryConfig, CollectionsWithSegmentsProviderConfig,
    },
    Frontend, FrontendConfig,
};
use chroma_log::config::{LogConfig, SqliteLogConfig};
use chroma_segment::local_segment_manager::LocalSegmentManagerConfig;
use chroma_sqlite::config::{MigrationHash, MigrationMode, SqliteDBConfig};
use chroma_sysdb::{SqliteSysDbConfig, SysDbConfig};
use chroma_system::System;
use chroma_types::{
    Collection, CollectionConfiguration, CollectionMetadataUpdate, CountCollectionsRequest,
    CreateCollectionRequest, CreateDatabaseRequest, Database, DeleteCollectionRequest,
    GetCollectionRequest, GetDatabaseRequest, GetResponse, IncludeList, InternalCollectionConfiguration,
    KnnIndex, ListCollectionsRequest, ListDatabasesRequest, Metadata, QueryRequest, QueryResponse,
    RawWhereFields, UpdateCollectionConfiguration, UpdateCollectionRequest, UpdateMetadata,
};
use libc::{c_char, c_double, c_float, c_int, c_uchar, c_uint, size_t};
use std::ffi::{CStr, CString};
use std::ptr;
use std::time::SystemTime;
use tokio::runtime::Runtime;

const DEFAULT_DATABASE: &str = "default_database";
const DEFAULT_TENANT: &str = "default_tenant";

// ===== Error Handling =====

/// Error codes for ChromaDB C API
#[repr(C)]
pub enum ChromaErrorCode {
    Success = 0,
    InvalidArgument = 1,
    InternalError = 2,
    MemoryError = 3,
    NotFound = 4,
    ValidationError = 5,
    InvalidUuid = 6,
    NotImplemented = 7,
}

/// Client handle for ChromaDB
#[repr(C)]
pub struct ChromaClient {
    runtime: Runtime,
    frontend: Frontend,
}

/// Collection handle for ChromaDB
#[repr(C)]
pub struct ChromaCollection {
    id: String,
    tenant: String,
    database: String,
}

/// Configuration for SQLite database
#[repr(C)]
pub struct SqliteConfigFFI {
    url: *const c_char,
    hash_type: c_int,
    migration_mode: c_int,
}

/// Response from query operations
#[repr(C)]
pub struct ChromaQueryResult {
    ids: *mut *mut c_char,
    ids_count: size_t,
    distances: *mut *mut c_float,
    distances_count: size_t,
    metadata_json: *mut *mut c_char,
    metadata_count: size_t,
    documents: *mut *mut c_char,
    documents_count: size_t,
}

/// Embedding vector
#[repr(C)]
pub struct ChromaEmbedding {
    values: *const c_float,
    dimension: size_t,
}

/// Result set information for ChromaDB operations
#[repr(C)]
pub struct ChromaResultSet {
    ids: *mut *mut c_char,
    count: size_t,
}

// ===== Helper Functions =====

/// Converts a C string to a Rust string
unsafe fn c_str_to_string(s: *const c_char) -> Result<String> {
    if s.is_null() {
        return Err(anyhow!("Null string pointer"));
    }
    
    Ok(CStr::from_ptr(s).to_string_lossy().into_owned())
}

/// Converts a Rust string to a C string
fn string_to_c_str(s: String) -> *mut c_char {
    match CString::new(s) {
        Ok(c_string) => c_string.into_raw(),
        Err(_) => ptr::null_mut(),
    }
}

/// Frees memory allocated for C strings
#[no_mangle]
pub extern "C" fn chroma_free_string(s: *mut c_char) {
    if !s.is_null() {
        unsafe {
            let _ = CString::from_raw(s);
        }
    }
}

/// Frees memory allocated for C string arrays
#[no_mangle]
pub extern "C" fn chroma_free_string_array(array: *mut *mut c_char, count: size_t) {
    if !array.is_null() {
        unsafe {
            for i in 0..count {
                let ptr = *array.add(i);
                if !ptr.is_null() {
                    let _ = CString::from_raw(ptr);
                }
            }
            libc::free(array as *mut libc::c_void);
        }
    }
}

/// Frees memory allocated for ChromaQueryResult
#[no_mangle]
pub extern "C" fn chroma_free_query_result(result: *mut ChromaQueryResult) {
    if !result.is_null() {
        unsafe {
            let result = &mut *result;
            
            chroma_free_string_array(result.ids, result.ids_count);
            
            if !result.distances.is_null() {
                libc::free(result.distances as *mut libc::c_void);
            }
            
            chroma_free_string_array(result.metadata_json, result.metadata_count);
            chroma_free_string_array(result.documents, result.documents_count);
            
            libc::free(result as *mut ChromaQueryResult as *mut libc::c_void);
        }
    }
}

/// Converts a Rust vector of strings to a C string array
fn vec_string_to_c_array(strings: Vec<String>) -> (*mut *mut c_char, size_t) {
    let count = strings.len();
    if count == 0 {
        return (ptr::null_mut(), 0);
    }

    unsafe {
        let array = libc::malloc(count * std::mem::size_of::<*mut c_char>()) as *mut *mut c_char;
        if array.is_null() {
            return (ptr::null_mut(), 0);
        }

        for (i, s) in strings.into_iter().enumerate() {
            *array.add(i) = string_to_c_str(s);
        }

        (array, count)
    }
}

/// Converts a C string array to a Rust vector of strings
unsafe fn c_array_to_vec_string(array: *const *const c_char, count: size_t) -> Result<Vec<String>> {
    if array.is_null() {
        return Ok(Vec::new());
    }

    let mut result = Vec::with_capacity(count);
    for i in 0..count {
        let c_str = *array.add(i);
        if !c_str.is_null() {
            result.push(c_str_to_string(c_str)?);
        } else {
            result.push(String::new());
        }
    }

    Ok(result)
}

/// Converts a C float array to a Rust vector of f32 values
unsafe fn c_array_to_vec_f32(array: *const c_float, count: size_t) -> Vec<f32> {
    if array.is_null() {
        return Vec::new();
    }

    let slice = std::slice::from_raw_parts(array, count);
    slice.to_vec()
}

/// Converts a Rust vector of f32 values to a C float array
fn vec_f32_to_c_array(values: Vec<f32>) -> (*mut c_float, size_t) {
    let count = values.len();
    if count == 0 {
        return (ptr::null_mut(), 0);
    }

    unsafe {
        let array = libc::malloc(count * std::mem::size_of::<c_float>()) as *mut c_float;
        if array.is_null() {
            return (ptr::null_mut(), 0);
        }

        for (i, &value) in values.iter().enumerate() {
            *array.add(i) = value;
        }

        (array, count)
    }
}

// ===== Client API Functions =====

/// Creates a new ChromaDB client
#[no_mangle]
pub extern "C" fn chroma_create_client(
    allow_reset: bool,
    sqlite_config_ptr: *const SqliteConfigFFI,
    hnsw_cache_size: size_t,
    persist_path_ptr: *const c_char,
    client_handle: *mut *mut ChromaClient,
) -> c_int {
    if client_handle.is_null() {
        return ChromaErrorCode::InvalidArgument as c_int;
    }

    // Parse SQLite configuration
    let sqlite_db_config = if !sqlite_config_ptr.is_null() {
        unsafe {
            let sqlite_config = &*sqlite_config_ptr;
            
            let url = match c_str_to_string(sqlite_config.url) {
                Ok(s) => s,
                Err(_) => return ChromaErrorCode::InvalidArgument as c_int,
            };
            
            let hash_type = match sqlite_config.hash_type {
                0 => MigrationHash::SHA256,
                1 => MigrationHash::MD5,
                _ => return ChromaErrorCode::InvalidArgument as c_int,
            };
            
            let migration_mode = match sqlite_config.migration_mode {
                0 => MigrationMode::Apply,
                1 => MigrationMode::Validate,
                _ => return ChromaErrorCode::InvalidArgument as c_int,
            };
            
            SqliteDBConfig {
                url,
                hash_type,
                migration_mode,
            }
        }
    } else {
        // Default SQLite configuration
        SqliteDBConfig {
            url: "sqlite:///chroma.db".to_string(),
            hash_type: MigrationHash::SHA256,
            migration_mode: MigrationMode::Apply,
        }
    };

    // Parse persistence path
    let persist_path = if !persist_path_ptr.is_null() {
        unsafe {
            match c_str_to_string(persist_path_ptr) {
                Ok(s) => Some(s),
                Err(_) => return ChromaErrorCode::InvalidArgument as c_int,
            }
        }
    } else {
        None
    };

    // Create runtime and frontend
    let runtime = match Runtime::new() {
        Ok(rt) => rt,
        Err(_) => return ChromaErrorCode::InternalError as c_int,
    };

    let _guard = runtime.enter();
    let system = System::new();
    let registry = Registry::new();

    // Configure cache
    let cache_config = FoyerCacheConfig {
        capacity: hnsw_cache_size,
        ..Default::default()
    };
    let cache_config = chroma_cache::CacheConfig::Memory(cache_config);
    
    // Configure segment manager
    let segment_manager_config = LocalSegmentManagerConfig {
        hnsw_index_pool_cache_config: cache_config,
        persist_path,
    };

    // Configure sysdb
    let sysdb_config = SysDbConfig::Sqlite(SqliteSysDbConfig {
        log_topic_namespace: "default".to_string(),
        log_tenant: "default".to_string(),
    });

    // Configure log
    let log_config = LogConfig::Sqlite(SqliteLogConfig {
        tenant_id: "default".to_string(),
        topic_namespace: "default".to_string(),
    });

    // Configure collection cache
    let collection_cache_config = CollectionsWithSegmentsProviderConfig {
        cache_invalidation_retry_policy: CacheInvalidationRetryConfig::new(0, 0),
        permitted_parallelism: 32,
        cache: chroma_cache::CacheConfig::Nop,
        cache_ttl_secs: 60,
    };

    // Configure executor
    let executor_config = ExecutorConfig::Local(LocalExecutorConfig {});

    // Default KNN index
    let knn_index = KnnIndex::Hnsw;

    // Build frontend config
    let frontend_config = FrontendConfig {
        allow_reset,
        segment_manager: Some(segment_manager_config),
        sqlitedb: Some(sqlite_db_config),
        sysdb: sysdb_config,
        collections_with_segments_provider: collection_cache_config,
        log: log_config,
        executor: executor_config,
        default_knn_index: knn_index,
    };

    // Create frontend
    let frontend = match runtime.block_on(async {
        Frontend::try_from_config(&(frontend_config, system), &registry).await
    }) {
        Ok(frontend) => frontend,
        Err(_) => return ChromaErrorCode::InternalError as c_int,
    };

    // Create client handle
    let client = Box::new(ChromaClient { runtime, frontend });
    unsafe {
        *client_handle = Box::into_raw(client);
    }

    ChromaErrorCode::Success as c_int
}

/// Destroys a ChromaDB client
#[no_mangle]
pub extern "C" fn chroma_destroy_client(client_handle: *mut ChromaClient) -> c_int {
    if !client_handle.is_null() {
        unsafe {
            let _ = Box::from_raw(client_handle);
        }
        ChromaErrorCode::Success as c_int
    } else {
        ChromaErrorCode::InvalidArgument as c_int
    }
}

/// Returns a heartbeat (current time) from the client
#[no_mangle]
pub extern "C" fn chroma_heartbeat(client_handle: *mut ChromaClient, result: *mut u64) -> c_int {
    if client_handle.is_null() || result.is_null() {
        return ChromaErrorCode::InvalidArgument as c_int;
    }

    let duration_since_epoch = match SystemTime::now().duration_since(SystemTime::UNIX_EPOCH) {
        Ok(duration) => duration,
        Err(_) => return ChromaErrorCode::InternalError as c_int,
    };

    unsafe {
        *result = duration_since_epoch.as_nanos() as u64;
    }

    ChromaErrorCode::Success as c_int
}

/// Creates a new database in ChromaDB
#[no_mangle]
pub extern "C" fn chroma_create_database(
    client_handle: *mut ChromaClient,
    name_ptr: *const c_char,
    tenant_ptr: *const c_char,
) -> c_int {
    if client_handle.is_null() || name_ptr.is_null() {
        return ChromaErrorCode::InvalidArgument as c_int;
    }

    let name = unsafe {
        match c_str_to_string(name_ptr) {
            Ok(s) => s,
            Err(_) => return ChromaErrorCode::InvalidArgument as c_int,
        }
    };

    let tenant = if !tenant_ptr.is_null() {
        unsafe {
            match c_str_to_string(tenant_ptr) {
                Ok(s) => s,
                Err(_) => return ChromaErrorCode::InvalidArgument as c_int,
            }
        }
    } else {
        DEFAULT_TENANT.to_string()
    };

    let client = unsafe { &mut *client_handle };
    
    let request = match CreateDatabaseRequest::try_new(tenant, name) {
        Ok(req) => req,
        Err(_) => return ChromaErrorCode::ValidationError as c_int,
    };

    let mut frontend = client.frontend.clone();
    
    match client.runtime.block_on(async { frontend.create_database(request).await }) {
        Ok(_) => ChromaErrorCode::Success as c_int,
        Err(_) => ChromaErrorCode::InternalError as c_int,
    }
}

/// Gets a database from ChromaDB
#[no_mangle]
pub extern "C" fn chroma_get_database(
    client_handle: *mut ChromaClient,
    name_ptr: *const c_char,
    tenant_ptr: *const c_char,
    id_result: *mut *mut c_char,
) -> c_int {
    if client_handle.is_null() || name_ptr.is_null() || id_result.is_null() {
        return ChromaErrorCode::InvalidArgument as c_int;
    }

    let name = unsafe {
        match c_str_to_string(name_ptr) {
            Ok(s) => s,
            Err(_) => return ChromaErrorCode::InvalidArgument as c_int,
        }
    };

    let tenant = if !tenant_ptr.is_null() {
        unsafe {
            match c_str_to_string(tenant_ptr) {
                Ok(s) => s,
                Err(_) => return ChromaErrorCode::InvalidArgument as c_int,
            }
        }
    } else {
        DEFAULT_TENANT.to_string()
    };

    let client = unsafe { &mut *client_handle };
    
    let request = match GetDatabaseRequest::try_new(tenant, name) {
        Ok(req) => req,
        Err(_) => return ChromaErrorCode::ValidationError as c_int,
    };

    let mut frontend = client.frontend.clone();
    
    match client.runtime.block_on(async { frontend.get_database(request).await }) {
        Ok(database) => {
            unsafe {
                *id_result = string_to_c_str(database.id.to_string());
            }
            ChromaErrorCode::Success as c_int
        },
        Err(_) => ChromaErrorCode::NotFound as c_int,
    }
}

/// Deletes a database from ChromaDB
#[no_mangle]
pub extern "C" fn chroma_delete_database(
    client_handle: *mut ChromaClient,
    name_ptr: *const c_char,
    tenant_ptr: *const c_char,
) -> c_int {
    if client_handle.is_null() || name_ptr.is_null() {
        return ChromaErrorCode::InvalidArgument as c_int;
    }

    let name = unsafe {
        match c_str_to_string(name_ptr) {
            Ok(s) => s,
            Err(_) => return ChromaErrorCode::InvalidArgument as c_int,
        }
    };

    let tenant = if !tenant_ptr.is_null() {
        unsafe {
            match c_str_to_string(tenant_ptr) {
                Ok(s) => s,
                Err(_) => return ChromaErrorCode::InvalidArgument as c_int,
            }
        }
    } else {
        DEFAULT_TENANT.to_string()
    };

    let client = unsafe { &mut *client_handle };
    
    let request = match DeleteDatabaseRequest::try_new(tenant, name) {
        Ok(req) => req,
        Err(_) => return ChromaErrorCode::ValidationError as c_int,
    };

    let mut frontend = client.frontend.clone();
    
    match client.runtime.block_on(async { frontend.delete_database(request).await }) {
        Ok(_) => ChromaErrorCode::Success as c_int,
        Err(_) => ChromaErrorCode::InternalError as c_int,
    }
}

// ===== Collection API Functions =====

/// Creates a new collection in ChromaDB
#[no_mangle]
pub extern "C" fn chroma_create_collection(
    client_handle: *mut ChromaClient,
    name_ptr: *const c_char,
    config_json_ptr: *const c_char,
    metadata_json_ptr: *const c_char,
    get_or_create: bool,
    tenant_ptr: *const c_char,
    database_ptr: *const c_char,
    collection_handle: *mut *mut ChromaCollection,
) -> c_int {
    if client_handle.is_null() || name_ptr.is_null() || collection_handle.is_null() {
        return ChromaErrorCode::InvalidArgument as c_int;
    }

    let name = unsafe {
        match c_str_to_string(name_ptr) {
            Ok(s) => s,
            Err(_) => return ChromaErrorCode::InvalidArgument as c_int,
        }
    };

    let tenant = if !tenant_ptr.is_null() {
        unsafe {
            match c_str_to_string(tenant_ptr) {
                Ok(s) => s,
                Err(_) => return ChromaErrorCode::InvalidArgument as c_int,
            }
        }
    } else {
        DEFAULT_TENANT.to_string()
    };

    let database = if !database_ptr.is_null() {
        unsafe {
            match c_str_to_string(database_ptr) {
                Ok(s) => s,
                Err(_) => return ChromaErrorCode::InvalidArgument as c_int,
            }
        }
    } else {
        DEFAULT_DATABASE.to_string()
    };

    // Parse configuration JSON if provided
    let configuration_json = if !config_json_ptr.is_null() {
        unsafe {
            let config_json_str = match c_str_to_string(config_json_ptr) {
                Ok(s) => s,
                Err(_) => return ChromaErrorCode::InvalidArgument as c_int,
            };
            
            match serde_json::from_str::<CollectionConfiguration>(&config_json_str) {
                Ok(config) => Some(config),
                Err(_) => return ChromaErrorCode::ValidationError as c_int,
            }
        }
    } else {
        None
    };

    // Parse metadata JSON if provided
    let metadata = if !metadata_json_ptr.is_null() {
        unsafe {
            let metadata_json_str = match c_str_to_string(metadata_json_ptr) {
                Ok(s) => s,
                Err(_) => return ChromaErrorCode::InvalidArgument as c_int,
            };
            
            match serde_json::from_str::<Metadata>(&metadata_json_str) {
                Ok(metadata) => Some(metadata),
                Err(_) => return ChromaErrorCode::ValidationError as c_int,
            }
        }
    } else {
        None
    };

    let client = unsafe { &mut *client_handle };

    // Convert configuration to internal format
    let configuration = match configuration_json {
        Some(c) => {
            match InternalCollectionConfiguration::try_from_config(
                c, 
                client.frontend.get_default_knn_index(),
            ) {
                Ok(config) => Some(config),
                Err(_) => return ChromaErrorCode::ValidationError as c_int,
            }
        },
        None => None,
    };

    let request = match CreateCollectionRequest::try_new(
        tenant.clone(),
        database.clone(),
        name,
        metadata,
        configuration,
        get_or_create,
    ) {
        Ok(req) => req,
        Err(_) => return ChromaErrorCode::ValidationError as c_int,
    };

    let mut frontend = client.frontend.clone();
    
    match client.runtime.block_on(async { frontend.create_collection(request).await }) {
        Ok(collection) => {
            let collection_wrapper = Box::new(ChromaCollection {
                id: collection.id.0.to_string(),
                tenant,
                database,
            });
            
            unsafe {
                *collection_handle = Box::into_raw(collection_wrapper);
            }
            
            ChromaErrorCode::Success as c_int
        },
        Err(_) => ChromaErrorCode::InternalError as c_int,
    }
}

/// Gets a collection from ChromaDB
#[no_mangle]
pub extern "C" fn chroma_get_collection(
    client_handle: *mut ChromaClient,
    name_ptr: *const c_char,
    tenant_ptr: *const c_char,
    database_ptr: *const c_char,
    collection_handle: *mut *mut ChromaCollection,
) -> c_int {
    if client_handle.is_null() || name_ptr.is_null() || collection_handle.is_null() {
        return ChromaErrorCode::InvalidArgument as c_int;
    }

    let name = unsafe {
        match c_str_to_string(name_ptr) {
            Ok(s) => s,
            Err(_) => return ChromaErrorCode::InvalidArgument as c_int,
        }
    };

    let tenant = if !tenant_ptr.is_null() {
        unsafe {
            match c_str_to_string(tenant_ptr) {
                Ok(s) => s,
                Err(_) => return ChromaErrorCode::InvalidArgument as c_int,
            }
        }
    } else {
        DEFAULT_TENANT.to_string()
    };

    let database = if !database_ptr.is_null() {
        unsafe {
            match c_str_to_string(database_ptr) {
                Ok(s) => s,
                Err(_) => return ChromaErrorCode::InvalidArgument as c_int,
            }
        }
    } else {
        DEFAULT_DATABASE.to_string()
    };

    let client = unsafe { &mut *client_handle };
    
    let request = match GetCollectionRequest::try_new(tenant.clone(), database.clone(), name) {
        Ok(req) => req,
        Err(_) => return ChromaErrorCode::ValidationError as c_int,
    };

    let mut frontend = client.frontend.clone();
    
    match client.runtime.block_on(async { frontend.get_collection(request).await }) {
        Ok(collection) => {
            let collection_wrapper = Box::new(ChromaCollection {
                id: collection.id.0.to_string(),
                tenant,
                database,
            });
            
            unsafe {
                *collection_handle = Box::into_raw(collection_wrapper);
            }
            
            ChromaErrorCode::Success as c_int
        },
        Err(_) => ChromaErrorCode::NotFound as c_int,
    }
}

/// Destroys a ChromaDB collection handle
#[no_mangle]
pub extern "C" fn chroma_destroy_collection(collection_handle: *mut ChromaCollection) -> c_int {
    if !collection_handle.is_null() {
        unsafe {
            let _ = Box::from_raw(collection_handle);
        }
        ChromaErrorCode::Success as c_int
    } else {
        ChromaErrorCode::InvalidArgument as c_int
    }
}

// ===== Document Management Functions =====

/// Adds documents to a collection
#[no_mangle]
pub extern "C" fn chroma_add(
    client_handle: *mut ChromaClient,
    collection_handle: *const ChromaCollection,
    ids: *const *const c_char,
    ids_count: size_t,
    embeddings: *const *const c_float,
    embedding_dim: size_t,
    metadatas_json: *const *const c_char,
    documents: *const *const c_char,
) -> c_int {
    if client_handle.is_null() || collection_handle.is_null() || ids.is_null() || ids_count == 0 {
        return ChromaErrorCode::InvalidArgument as c_int;
    }

    let client = unsafe { &mut *client_handle };
    let collection = unsafe { &*collection_handle };

    // Convert C string array to Rust vector
    let ids_vec = unsafe {
        match c_array_to_vec_string(ids, ids_count) {
            Ok(v) => v,
            Err(_) => return ChromaErrorCode::InvalidArgument as c_int,
        }
    };

    // Convert C embedding array to Rust vector
    let embeddings_vec = if !embeddings.is_null() {
        let mut result = Vec::with_capacity(ids_count);
        unsafe {
            for i in 0..ids_count {
                let embedding_ptr = *embeddings.add(i);
                if !embedding_ptr.is_null() {
                    result.push(c_array_to_vec_f32(embedding_ptr, embedding_dim));
                } else {
                    return ChromaErrorCode::InvalidArgument as c_int;
                }
            }
        }
        Some(result)
    } else {
        return ChromaErrorCode::InvalidArgument as c_int;
    };

    // Convert metadata JSON strings to Rust vector
    let metadatas_vec = if !metadatas_json.is_null() {
        let mut result = Vec::with_capacity(ids_count);
        unsafe {
            for i in 0..ids_count {
                let metadata_ptr = *metadatas_json.add(i);
                if !metadata_ptr.is_null() {
                    let metadata_str = match c_str_to_string(metadata_ptr) {
                        Ok(s) => s,
                        Err(_) => return ChromaErrorCode::InvalidArgument as c_int,
                    };
                    
                    if metadata_str.is_empty() {
                        result.push(None);
                    } else {
                        match serde_json::from_str::<Metadata>(&metadata_str) {
                            Ok(metadata) => result.push(Some(metadata)),
                            Err(_) => return ChromaErrorCode::ValidationError as c_int,
                        }
                    }
                } else {
                    result.push(None);
                }
            }
        }
        Some(result)
    } else {
        None
    };

    // Convert document strings to Rust vector
    let documents_vec = if !documents.is_null() {
        let mut result = Vec::with_capacity(ids_count);
        unsafe {
            for i in 0..ids_count {
                let document_ptr = *documents.add(i);
                if !document_ptr.is_null() {
                    match c_str_to_string(document_ptr) {
                        Ok(s) => {
                            if s.is_empty() {
                                result.push(None);
                            } else {
                                result.push(Some(s));
                            }
                        },
                        Err(_) => return ChromaErrorCode::InvalidArgument as c_int,
                    }
                } else {
                    result.push(None);
                }
            }
        }
        Some(result)
    } else {
        None
    };

    // Parse collection ID
    let collection_id = match uuid::Uuid::parse_str(&collection.id) {
        Ok(id) => chroma_types::CollectionUuid(id),
        Err(_) => return ChromaErrorCode::InvalidUuid as c_int,
    };

    // Create request
    let request = match chroma_types::AddCollectionRecordsRequest::try_new(
        collection.tenant.clone(),
        collection.database.clone(),
        collection_id,
        ids_vec,
        embeddings_vec,
        documents_vec,
        None, // uris
        metadatas_vec,
    ) {
        Ok(req) => req,
        Err(_) => return ChromaErrorCode::ValidationError as c_int,
    };

    // Execute request
    let mut frontend = client.frontend.clone();
    match client.runtime.block_on(async { frontend.add(request).await }) {
        Ok(_) => ChromaErrorCode::Success as c_int,
        Err(_) => ChromaErrorCode::InternalError as c_int,
    }
}

/// Queries a collection for similar documents
#[no_mangle]
pub extern "C" fn chroma_query(
    client_handle: *mut ChromaClient,
    collection_handle: *const ChromaCollection,
    query_embeddings: *const c_float,
    embedding_dim: size_t,
    n_results: c_uint,
    where_filter_json: *const c_char,
    where_document_filter: *const c_char,
    include_embeddings: bool,
    include_metadatas: bool,
    include_documents: bool,
    include_distances: bool,
    result: *mut *mut ChromaQueryResult,
) -> c_int {
    if client_handle.is_null() || collection_handle.is_null() || query_embeddings.is_null() || result.is_null() {
        return ChromaErrorCode::InvalidArgument as c_int;
    }

    let client = unsafe { &mut *client_handle };
    let collection = unsafe { &*collection_handle };

    // Parse collection ID
    let collection_id = match uuid::Uuid::parse_str(&collection.id) {
        Ok(id) => chroma_types::CollectionUuid(id),
        Err(_) => return ChromaErrorCode::InvalidUuid as c_int,
    };

    // Convert C embedding to Rust vector
    let query_embedding_vec = unsafe {
        vec![c_array_to_vec_f32(query_embeddings, embedding_dim)]
    };

    // Parse where filters
    let where_filter = unsafe {
        let where_json_str = if !where_filter_json.is_null() {
            match c_str_to_string(where_filter_json) {
                Ok(s) => Some(s),
                Err(_) => return ChromaErrorCode::InvalidArgument as c_int,
            }
        } else {
            None
        };

        let where_document = if !where_document_filter.is_null() {
            match c_str_to_string(where_document_filter) {
                Ok(s) => Some(s),
                Err(_) => return ChromaErrorCode::InvalidArgument as c_int,
            }
        } else {
            None
        };

        match RawWhereFields::from_json_str(where_json_str.as_deref(), where_document.as_deref()) {
            Ok(raw) => match raw.parse() {
                Ok(parsed) => parsed,
                Err(_) => return ChromaErrorCode::ValidationError as c_int,
            },
            Err(_) => return ChromaErrorCode::ValidationError as c_int,
        }
    };

    // Build include list
    let mut include = Vec::new();
    if include_embeddings {
        include.push("embeddings".to_string());
    }
    if include_metadatas {
        include.push("metadatas".to_string());
    }
    if include_documents {
        include.push("documents".to_string());
    }
    if include_distances {
        include.push("distances".to_string());
    }

    let include_list = match IncludeList::try_from(include) {
        Ok(list) => list,
        Err(_) => return ChromaErrorCode::ValidationError as c_int,
    };

    // Create query request
    let request = match QueryRequest::try_new(
        collection.tenant.clone(),
        collection.database.clone(),
        collection_id,
        None, // ids
        where_filter,
        query_embedding_vec,
        n_results,
        include_list,
    ) {
        Ok(req) => req,
        Err(_) => return ChromaErrorCode::ValidationError as c_int,
    };

    // Execute query
    let mut frontend = client.frontend.clone();
    let query_response = match client.runtime.block_on(async { frontend.query(request).await }) {
        Ok(resp) => resp,
        Err(_) => return ChromaErrorCode::InternalError as c_int,
    };

    // Convert query response to C struct
    let query_result = Box::new(ChromaQueryResult {
        ids: ptr::null_mut(),
        ids_count: 0,
        distances: ptr::null_mut(),
        distances_count: 0,
        metadata_json: ptr::null_mut(),
        metadata_count: 0,
        documents: ptr::null_mut(),
        documents_count: 0,
    });

    let query_result_ptr = Box::into_raw(query_result);
    let query_result = unsafe { &mut *query_result_ptr };

    // Set IDs
    if !query_response.ids.is_empty() {
        let ids = query_response.ids[0].clone();
        let (array, count) = vec_string_to_c_array(ids);
        query_result.ids = array;
        query_result.ids_count = count;
    }

    // Set distances if available
    if let Some(distances) = query_response.distances {
        if !distances.is_empty() {
            let distance_vec = distances[0].clone();
            let (array, count) = vec_f32_to_c_array(distance_vec);
            query_result.distances = array;
            query_result.distances_count = count;
        }
    }

    // Set metadata if available
    if let Some(metadatas) = query_response.metadatas {
        if !metadatas.is_empty() {
            let metadata_strings: Vec<String> = metadatas[0]
                .iter()
                .map(|m| match m {
                    Some(metadata) => serde_json::to_string(metadata).unwrap_or_default(),
                    None => String::new(),
                })
                .collect();
            
            let (array, count) = vec_string_to_c_array(metadata_strings);
            query_result.metadata_json = array;
            query_result.metadata_count = count;
        }
    }

    // Set documents if available
    if let Some(documents) = query_response.documents {
        if !documents.is_empty() {
            let doc_strings: Vec<String> = documents[0]
                .iter()
                .map(|d| d.clone().unwrap_or_default())
                .collect();
            
            let (array, count) = vec_string_to_c_array(doc_strings);
            query_result.documents = array;
            query_result.documents_count = count;
        }
    }

    unsafe {
        *result = query_result_ptr;
    }

    ChromaErrorCode::Success as c_int
}