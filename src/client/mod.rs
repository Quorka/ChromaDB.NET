// Client module for ChromaDB C# bindings
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
    CreateDatabaseRequest, DeleteDatabaseRequest, GetDatabaseRequest, KnnIndex,
};
use libc::{c_char, c_int, size_t};
use std::time::SystemTime;
use tokio::runtime::Runtime;

use crate::error::{set_error, set_success, ChromaErrorCode, ChromaError};
use crate::types::SqliteConfigFFI;
use crate::utils::{c_str_to_string, string_to_c_str, DEFAULT_TENANT};

/// Client handle for ChromaDB
#[repr(C)]
pub struct ChromaClient {
    pub(crate) runtime: Runtime,
    pub(crate) frontend: Frontend,
}

/// Creates a new ChromaDB client
#[no_mangle]
pub extern "C" fn chroma_create_client(
    allow_reset: bool,
    sqlite_config_ptr: *const SqliteConfigFFI,
    hnsw_cache_size: size_t,
    persist_path_ptr: *const c_char,
    client_handle: *mut *mut ChromaClient,
    error_out: *mut *mut ChromaError,
) -> c_int {
    // Function name for error reporting
    let func_name = "chroma_create_client";

    // Check arguments
    if client_handle.is_null() {
        set_error(
            error_out,
            ChromaErrorCode::InvalidArgument,
            "Client handle pointer is null",
            func_name,
            None,
        );
        return ChromaErrorCode::InvalidArgument as c_int;
    }

    // Parse SQLite configuration
    let sqlite_db_config = if !sqlite_config_ptr.is_null() {
        unsafe {
            let sqlite_config = &*sqlite_config_ptr;

            let url = match c_str_to_string(sqlite_config.url) {
                Ok(s) => s,
                Err(e) => {
                    set_error(
                        error_out,
                        ChromaErrorCode::InvalidArgument,
                        "Invalid SQLite URL",
                        func_name,
                        Some(&e.to_string()),
                    );
                    return ChromaErrorCode::InvalidArgument as c_int;
                }
            };

            let hash_type = match sqlite_config.hash_type {
                0 => MigrationHash::SHA256,
                1 => MigrationHash::MD5,
                invalid => {
                    set_error(
                        error_out,
                        ChromaErrorCode::InvalidArgument,
                        "Invalid hash type",
                        func_name,
                        Some(&format!("Got {}, expected 0 (SHA256) or 1 (MD5)", invalid)),
                    );
                    return ChromaErrorCode::InvalidArgument as c_int;
                }
            };

            let migration_mode = match sqlite_config.migration_mode {
                0 => MigrationMode::Apply,
                1 => MigrationMode::Validate,
                invalid => {
                    set_error(
                        error_out,
                        ChromaErrorCode::InvalidArgument,
                        "Invalid migration mode",
                        func_name,
                        Some(&format!(
                            "Got {}, expected 0 (Apply) or 1 (Validate)",
                            invalid
                        )),
                    );
                    return ChromaErrorCode::InvalidArgument as c_int;
                }
            };

            SqliteDBConfig {
                url: Some(url),
                hash_type,
                migration_mode,
            }
        }
    } else {
        // Default SQLite configuration
        SqliteDBConfig {
            url: Some("sqlite:///chroma.db".to_string()),
            hash_type: MigrationHash::SHA256,
            migration_mode: MigrationMode::Apply,
        }
    };

    // Parse persistence path
    let persist_path = if !persist_path_ptr.is_null() {
        unsafe {
            match c_str_to_string(persist_path_ptr) {
                Ok(s) => Some(s),
                Err(e) => {
                    set_error(
                        error_out,
                        ChromaErrorCode::InvalidArgument,
                        "Invalid persistence path",
                        func_name,
                        Some(&e.to_string()),
                    );
                    return ChromaErrorCode::InvalidArgument as c_int;
                }
            }
        }
    } else {
        None
    };

    // Create runtime and frontend
    let runtime = match Runtime::new() {
        Ok(rt) => rt,
        Err(e) => {
            set_error(
                error_out,
                ChromaErrorCode::InternalError,
                "Failed to create Tokio runtime",
                func_name,
                Some(&e.to_string()),
            );
            return ChromaErrorCode::InternalError as c_int;
        }
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
    let frontend = match runtime
        .block_on(async { Frontend::try_from_config(&(frontend_config, system), &registry).await })
    {
        Ok(frontend) => frontend,
        Err(e) => {
            set_error(
                error_out,
                ChromaErrorCode::InternalError,
                "Failed to create Chroma frontend",
                func_name,
                Some(&e.to_string()),
            );
            return ChromaErrorCode::InternalError as c_int;
        }
    };

    // Create client handle
    let client = Box::new(ChromaClient { runtime, frontend });
    unsafe {
        *client_handle = Box::into_raw(client);
    }

    // Create success result
    set_success(error_out);

    ChromaErrorCode::Success as c_int
}

/// Destroys a ChromaDB client
#[no_mangle]
pub extern "C" fn chroma_destroy_client(
    client_handle: *mut ChromaClient,
    error_out: *mut *mut ChromaError,
) -> c_int {
    let func_name = "chroma_destroy_client";

    if client_handle.is_null() {
        set_error(
            error_out,
            ChromaErrorCode::InvalidArgument,
            "Client handle pointer is null",
            func_name,
            None,
        );
        return ChromaErrorCode::InvalidArgument as c_int;
    }

    unsafe {
        let _ = Box::from_raw(client_handle);
    }

    // Return success
    set_success(error_out);

    ChromaErrorCode::Success as c_int
}

/// Returns a heartbeat (current time) from the client
#[no_mangle]
pub extern "C" fn chroma_heartbeat(
    client_handle: *mut ChromaClient,
    result: *mut u64,
    error_out: *mut *mut ChromaError,
) -> c_int {
    let func_name = "chroma_heartbeat";

    if client_handle.is_null() || result.is_null() {
        let message = if client_handle.is_null() {
            "Client handle pointer is null"
        } else {
            "Result pointer is null"
        };

        set_error(
            error_out,
            ChromaErrorCode::InvalidArgument,
            message,
            func_name,
            None,
        );
        return ChromaErrorCode::InvalidArgument as c_int;
    }

    let duration_since_epoch = match SystemTime::now().duration_since(SystemTime::UNIX_EPOCH) {
        Ok(duration) => duration,
        Err(e) => {
            set_error(
                error_out,
                ChromaErrorCode::InternalError,
                "Failed to get system time",
                func_name,
                Some(&e.to_string()),
            );
            return ChromaErrorCode::InternalError as c_int;
        }
    };

    unsafe {
        *result = duration_since_epoch.as_nanos() as u64;
    }

    // Return success
    set_success(error_out);

    ChromaErrorCode::Success as c_int
}

/// Creates a new database in ChromaDB
#[no_mangle]
pub extern "C" fn chroma_create_database(
    client_handle: *mut ChromaClient,
    name_ptr: *const c_char,
    tenant_ptr: *const c_char,
    error_out: *mut *mut ChromaError,
) -> c_int {
    let func_name = "chroma_create_database";

    // Check arguments
    if client_handle.is_null() || name_ptr.is_null() {
        let message = if client_handle.is_null() {
            "Client handle pointer is null"
        } else {
            "Database name pointer is null"
        };

        set_error(
            error_out,
            ChromaErrorCode::InvalidArgument,
            message,
            func_name,
            None,
        );
        return ChromaErrorCode::InvalidArgument as c_int;
    }

    // Parse name
    let name = unsafe {
        match c_str_to_string(name_ptr) {
            Ok(s) => s,
            Err(e) => {
                set_error(
                    error_out,
                    ChromaErrorCode::InvalidArgument,
                    "Invalid database name",
                    func_name,
                    Some(&e.to_string()),
                );
                return ChromaErrorCode::InvalidArgument as c_int;
            }
        }
    };

    // Parse tenant name
    let tenant = if !tenant_ptr.is_null() {
        unsafe {
            match c_str_to_string(tenant_ptr) {
                Ok(s) => s,
                Err(e) => {
                    set_error(
                        error_out,
                        ChromaErrorCode::InvalidArgument,
                        "Invalid tenant name",
                        func_name,
                        Some(&e.to_string()),
                    );
                    return ChromaErrorCode::InvalidArgument as c_int;
                }
            }
        }
    } else {
        DEFAULT_TENANT.to_string()
    };

    // Get client reference
    let client = unsafe { &mut *client_handle };

    // Create database request
    let request = match CreateDatabaseRequest::try_new(tenant, name) {
        Ok(req) => req,
        Err(e) => {
            set_error(
                error_out,
                ChromaErrorCode::ValidationError,
                "Failed to create database request",
                func_name,
                Some(&format!("Validation error: {:?}", e)),
            );
            return ChromaErrorCode::ValidationError as c_int;
        }
    };

    // Execute request
    let mut frontend = client.frontend.clone();

    match client
        .runtime
        .block_on(async { frontend.create_database(request).await })
    {
        Ok(_) => {
            // Return success
            set_success(error_out);
            ChromaErrorCode::Success as c_int
        }
        Err(e) => {
            set_error(
                error_out,
                ChromaErrorCode::InternalError,
                "Failed to create database",
                func_name,
                Some(&format!("Error: {:?}", e)),
            );
            ChromaErrorCode::InternalError as c_int
        }
    }
}

/// Gets a database from ChromaDB
#[no_mangle]
pub extern "C" fn chroma_get_database(
    client_handle: *mut ChromaClient,
    name_ptr: *const c_char,
    tenant_ptr: *const c_char,
    id_result: *mut *mut c_char,
    error_out: *mut *mut ChromaError,
) -> c_int {
    let func_name = "chroma_get_database";

    if client_handle.is_null() || name_ptr.is_null() || id_result.is_null() {
        let message = if client_handle.is_null() {
            "Client handle pointer is null"
        } else if name_ptr.is_null() {
            "Database name pointer is null"
        } else {
            "ID result pointer is null"
        };

        set_error(
            error_out,
            ChromaErrorCode::InvalidArgument,
            message,
            func_name,
            None,
        );
        return ChromaErrorCode::InvalidArgument as c_int;
    }

    let name = unsafe {
        match c_str_to_string(name_ptr) {
            Ok(s) => s,
            Err(e) => {
                set_error(
                    error_out,
                    ChromaErrorCode::InvalidArgument,
                    "Invalid database name",
                    func_name,
                    Some(&e.to_string()),
                );
                return ChromaErrorCode::InvalidArgument as c_int;
            }
        }
    };

    let tenant = if !tenant_ptr.is_null() {
        unsafe {
            match c_str_to_string(tenant_ptr) {
                Ok(s) => s,
                Err(e) => {
                    set_error(
                        error_out,
                        ChromaErrorCode::InvalidArgument,
                        "Invalid tenant name",
                        func_name,
                        Some(&e.to_string()),
                    );
                    return ChromaErrorCode::InvalidArgument as c_int;
                }
            }
        }
    } else {
        DEFAULT_TENANT.to_string()
    };

    let client = unsafe { &mut *client_handle };

    let request = match GetDatabaseRequest::try_new(tenant, name) {
        Ok(req) => req,
        Err(e) => {
            set_error(
                error_out,
                ChromaErrorCode::ValidationError,
                "Failed to create get database request",
                func_name,
                Some(&format!("Validation error: {:?}", e)),
            );
            return ChromaErrorCode::ValidationError as c_int;
        }
    };

    let mut frontend = client.frontend.clone();

    match client
        .runtime
        .block_on(async { frontend.get_database(request).await })
    {
        Ok(database) => {
            unsafe {
                *id_result = string_to_c_str(database.id.to_string());
            }
            set_success(error_out);
            ChromaErrorCode::Success as c_int
        }
        Err(e) => {
            set_error(
                error_out,
                ChromaErrorCode::NotFound,
                "Database not found",
                func_name,
                Some(&format!("Error: {:?}", e)),
            );
            ChromaErrorCode::NotFound as c_int
        }
    }
}

/// Deletes a database from ChromaDB
#[no_mangle]
pub extern "C" fn chroma_delete_database(
    client_handle: *mut ChromaClient,
    name_ptr: *const c_char,
    tenant_ptr: *const c_char,
    error_out: *mut *mut ChromaError,
) -> c_int {
    let func_name = "chroma_delete_database";

    if client_handle.is_null() || name_ptr.is_null() {
        let message = if client_handle.is_null() {
            "Client handle pointer is null"
        } else {
            "Database name pointer is null"
        };

        set_error(
            error_out,
            ChromaErrorCode::InvalidArgument,
            message,
            func_name,
            None,
        );
        return ChromaErrorCode::InvalidArgument as c_int;
    }

    let name = unsafe {
        match c_str_to_string(name_ptr) {
            Ok(s) => s,
            Err(e) => {
                set_error(
                    error_out,
                    ChromaErrorCode::InvalidArgument,
                    "Invalid database name",
                    func_name,
                    Some(&e.to_string()),
                );
                return ChromaErrorCode::InvalidArgument as c_int;
            }
        }
    };

    let tenant = if !tenant_ptr.is_null() {
        unsafe {
            match c_str_to_string(tenant_ptr) {
                Ok(s) => s,
                Err(e) => {
                    set_error(
                        error_out,
                        ChromaErrorCode::InvalidArgument,
                        "Invalid tenant name",
                        func_name,
                        Some(&e.to_string()),
                    );
                    return ChromaErrorCode::InvalidArgument as c_int;
                }
            }
        }
    } else {
        DEFAULT_TENANT.to_string()
    };

    let client = unsafe { &mut *client_handle };

    let request = match DeleteDatabaseRequest::try_new(tenant, name) {
        Ok(req) => req,
        Err(e) => {
            set_error(
                error_out,
                ChromaErrorCode::ValidationError,
                "Failed to create delete database request",
                func_name,
                Some(&format!("Validation error: {:?}", e)),
            );
            return ChromaErrorCode::ValidationError as c_int;
        }
    };

    let mut frontend = client.frontend.clone();

    match client
        .runtime
        .block_on(async { frontend.delete_database(request).await })
    {
        Ok(_) => {
            set_success(error_out);
            ChromaErrorCode::Success as c_int
        }
        Err(e) => {
            set_error(
                error_out,
                ChromaErrorCode::InternalError,
                "Failed to delete database",
                func_name,
                Some(&format!("Error: {:?}", e)),
            );
            ChromaErrorCode::InternalError as c_int
        }
    }
}
