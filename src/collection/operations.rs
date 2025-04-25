// Collection operations for ChromaDB C# bindings
use chroma_types::{
    AddCollectionRecordsRequest, CollectionUuid, CountRequest, DeleteCollectionRecordsRequest,
    GetRequest, IncludeList, Metadata, QueryRequest, RawWhereFields,
    UpdateCollectionRecordsRequest, UpdateMetadata, UpsertCollectionRecordsRequest,
};
use libc::{c_char, c_float, c_int, c_uint, size_t};
use std::ptr;
use uuid;

use crate::client::ChromaClient;
use crate::collection::types::ChromaCollection;
use crate::error::{set_error, set_success, ChromaError, ChromaErrorCode};
use crate::types::ChromaQueryResult;
use crate::utils::{
    c_array_to_vec_f32, c_array_to_vec_string, c_str_to_string, vec_f32_to_c_array,
    vec_string_to_c_array,
};

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
    error_out: *mut *mut ChromaError,
) -> c_int {
    let func_name = "chroma_add";

    // Check required parameters
    if client_handle.is_null() || collection_handle.is_null() || ids.is_null() || ids_count == 0 {
        let message = if client_handle.is_null() {
            "Client handle pointer is null"
        } else if collection_handle.is_null() {
            "Collection handle pointer is null"
        } else if ids.is_null() {
            "IDs pointer is null"
        } else {
            "IDs count is zero"
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

    let client = unsafe { &mut *client_handle };
    let collection = unsafe { &*collection_handle };

    // Convert C string array to Rust vector
    let ids_vec = unsafe {
        match c_array_to_vec_string(ids, ids_count) {
            Ok(v) => v,
            Err(e) => {
                set_error(
                    error_out,
                    ChromaErrorCode::InvalidArgument,
                    "Failed to convert IDs array",
                    func_name,
                    Some(&e.to_string()),
                );
                return ChromaErrorCode::InvalidArgument as c_int;
            }
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
                    set_error(
                        error_out,
                        ChromaErrorCode::InvalidArgument,
                        "Embedding pointer is null",
                        func_name,
                        Some(&format!("Null embedding at index {}", i)),
                    );
                    return ChromaErrorCode::InvalidArgument as c_int;
                }
            }
        }
        Some(result)
    } else {
        set_error(
            error_out,
            ChromaErrorCode::InvalidArgument,
            "Embeddings pointer is null",
            func_name,
            None,
        );
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
                        Err(e) => {
                            set_error(
                                error_out,
                                ChromaErrorCode::InvalidArgument,
                                "Failed to convert metadata string",
                                func_name,
                                Some(&format!("Error at index {}: {}", i, e)),
                            );
                            return ChromaErrorCode::InvalidArgument as c_int;
                        }
                    };

                    if metadata_str.is_empty() {
                        result.push(None);
                    } else {
                        match serde_json::from_str::<Metadata>(&metadata_str) {
                            Ok(metadata) => result.push(Some(metadata)),
                            Err(e) => {
                                set_error(
                                    error_out,
                                    ChromaErrorCode::ValidationError,
                                    "Invalid metadata JSON",
                                    func_name,
                                    Some(&format!("Error parsing metadata at index {}: {}", i, e)),
                                );
                                return ChromaErrorCode::ValidationError as c_int;
                            }
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
                        }
                        Err(e) => {
                            set_error(
                                error_out,
                                ChromaErrorCode::InvalidArgument,
                                "Failed to convert document string",
                                func_name,
                                Some(&format!("Error at index {}: {}", i, e)),
                            );
                            return ChromaErrorCode::InvalidArgument as c_int;
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

    // Parse collection ID
    let collection_id = match uuid::Uuid::parse_str(&collection.id) {
        Ok(id) => CollectionUuid(id),
        Err(e) => {
            set_error(
                error_out,
                ChromaErrorCode::InvalidUuid,
                "Invalid collection UUID",
                func_name,
                Some(&format!("UUID parse error: {}", e)),
            );
            return ChromaErrorCode::InvalidUuid as c_int;
        }
    };

    // Create request
    let request = match AddCollectionRecordsRequest::try_new(
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
        Err(e) => {
            set_error(
                error_out,
                ChromaErrorCode::ValidationError,
                "Failed to create add request",
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
        .block_on(async { frontend.add(request).await })
    {
        Ok(_) => {
            set_success(error_out);
            ChromaErrorCode::Success as c_int
        }
        Err(e) => {
            set_error(
                error_out,
                ChromaErrorCode::InternalError,
                "Failed to add documents",
                func_name,
                Some(&format!("Error: {:?}", e)),
            );
            ChromaErrorCode::InternalError as c_int
        }
    }
}

/// Counts the number of documents in a collection
#[no_mangle]
pub extern "C" fn chroma_count(
    client_handle: *mut ChromaClient,
    collection_handle: *const ChromaCollection,
    result: *mut c_uint,
    error_out: *mut *mut ChromaError,
) -> c_int {
    let func_name = "chroma_count";

    if client_handle.is_null() || collection_handle.is_null() || result.is_null() {
        let message = if client_handle.is_null() {
            "Client handle pointer is null"
        } else if collection_handle.is_null() {
            "Collection handle pointer is null"
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

    let client = unsafe { &mut *client_handle };
    let collection = unsafe { &*collection_handle };

    // Parse collection ID
    let collection_id = match uuid::Uuid::parse_str(&collection.id) {
        Ok(id) => CollectionUuid(id),
        Err(e) => {
            set_error(
                error_out,
                ChromaErrorCode::InvalidUuid,
                "Invalid collection UUID",
                func_name,
                Some(&format!("UUID parse error: {}", e)),
            );
            return ChromaErrorCode::InvalidUuid as c_int;
        }
    };

    // Create count request
    let request = match CountRequest::try_new(
        collection.tenant.clone(),
        collection.database.clone(),
        collection_id,
    ) {
        Ok(req) => req,
        Err(e) => {
            set_error(
                error_out,
                ChromaErrorCode::ValidationError,
                "Failed to create count request",
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
        .block_on(async { frontend.count(request).await })
    {
        Ok(count_response) => {
            unsafe {
                *result = count_response;
            }
            set_success(error_out);
            ChromaErrorCode::Success as c_int
        }
        Err(e) => {
            set_error(
                error_out,
                ChromaErrorCode::InternalError,
                "Failed to count documents",
                func_name,
                Some(&format!("Error: {:?}", e)),
            );
            ChromaErrorCode::InternalError as c_int
        }
    }
}

/// Updates documents in a collection
#[no_mangle]
pub extern "C" fn chroma_update(
    client_handle: *mut ChromaClient,
    collection_handle: *const ChromaCollection,
    ids: *const *const c_char,
    ids_count: size_t,
    embeddings: *const *const c_float,
    embedding_dim: size_t,
    metadatas_json: *const *const c_char,
    documents: *const *const c_char,
    error_out: *mut *mut ChromaError,
) -> c_int {
    let func_name = "chroma_update";

    if client_handle.is_null() || collection_handle.is_null() || ids.is_null() || ids_count == 0 {
        let message = if client_handle.is_null() {
            "Client handle pointer is null"
        } else if collection_handle.is_null() {
            "Collection handle pointer is null"
        } else if ids.is_null() {
            "IDs pointer is null"
        } else {
            "IDs count is zero"
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

    let client = unsafe { &mut *client_handle };
    let collection = unsafe { &*collection_handle };

    // Convert C string array to Rust vector
    let ids_vec = unsafe {
        match c_array_to_vec_string(ids, ids_count) {
            Ok(v) => v,
            Err(e) => {
                set_error(
                    error_out,
                    ChromaErrorCode::InvalidArgument,
                    "Failed to convert IDs array",
                    func_name,
                    Some(&e.to_string()),
                );
                return ChromaErrorCode::InvalidArgument as c_int;
            }
        }
    };

    // Convert C embedding array to Rust vector
    let embeddings_vec = if !embeddings.is_null() {
        let mut result = Vec::with_capacity(ids_count);
        unsafe {
            for i in 0..ids_count {
                let embedding_ptr = *embeddings.add(i);
                if !embedding_ptr.is_null() {
                    let vec = c_array_to_vec_f32(embedding_ptr, embedding_dim);
                    result.push(Some(vec));
                } else {
                    result.push(None);
                }
            }
        }
        Some(result)
    } else {
        None
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
                        Err(e) => {
                            set_error(
                                error_out,
                                ChromaErrorCode::InvalidArgument,
                                "Failed to convert metadata string",
                                func_name,
                                Some(&format!("Error at index {}: {}", i, e)),
                            );
                            return ChromaErrorCode::InvalidArgument as c_int;
                        }
                    };

                    if metadata_str.is_empty() {
                        result.push(None);
                    } else {
                        match serde_json::from_str::<UpdateMetadata>(&metadata_str) {
                            Ok(metadata) => result.push(Some(metadata)),
                            Err(e) => {
                                set_error(
                                    error_out,
                                    ChromaErrorCode::ValidationError,
                                    "Invalid metadata JSON",
                                    func_name,
                                    Some(&format!("Error parsing metadata at index {}: {}", i, e)),
                                );
                                return ChromaErrorCode::ValidationError as c_int;
                            }
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
                        }
                        Err(e) => {
                            set_error(
                                error_out,
                                ChromaErrorCode::InvalidArgument,
                                "Failed to convert document string",
                                func_name,
                                Some(&format!("Error at index {}: {}", i, e)),
                            );
                            return ChromaErrorCode::InvalidArgument as c_int;
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

    // Parse collection ID
    let collection_id = match uuid::Uuid::parse_str(&collection.id) {
        Ok(id) => CollectionUuid(id),
        Err(e) => {
            set_error(
                error_out,
                ChromaErrorCode::InvalidUuid,
                "Invalid collection UUID",
                func_name,
                Some(&format!("UUID parse error: {}", e)),
            );
            return ChromaErrorCode::InvalidUuid as c_int;
        }
    };

    // Create update request
    let request = match UpdateCollectionRecordsRequest::try_new(
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
        Err(e) => {
            set_error(
                error_out,
                ChromaErrorCode::ValidationError,
                "Failed to create update request",
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
        .block_on(async { frontend.update(request).await })
    {
        Ok(_) => {
            set_success(error_out);
            ChromaErrorCode::Success as c_int
        }
        Err(e) => {
            set_error(
                error_out,
                ChromaErrorCode::InternalError,
                "Failed to update documents",
                func_name,
                Some(&format!("Error: {:?}", e)),
            );
            ChromaErrorCode::InternalError as c_int
        }
    }
}

/// Upserts documents in a collection (adds if not exists, updates if exists)
#[no_mangle]
pub extern "C" fn chroma_upsert(
    client_handle: *mut ChromaClient,
    collection_handle: *const ChromaCollection,
    ids: *const *const c_char,
    ids_count: size_t,
    embeddings: *const *const c_float,
    embedding_dim: size_t,
    metadatas_json: *const *const c_char,
    documents: *const *const c_char,
    error_out: *mut *mut ChromaError,
) -> c_int {
    let func_name = "chroma_upsert";

    if client_handle.is_null() || collection_handle.is_null() || ids.is_null() || ids_count == 0 {
        let message = if client_handle.is_null() {
            "Client handle pointer is null"
        } else if collection_handle.is_null() {
            "Collection handle pointer is null"
        } else if ids.is_null() {
            "IDs pointer is null"
        } else {
            "IDs count is zero"
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

    let client = unsafe { &mut *client_handle };
    let collection = unsafe { &*collection_handle };

    // Convert C string array to Rust vector
    let ids_vec = unsafe {
        match c_array_to_vec_string(ids, ids_count) {
            Ok(v) => v,
            Err(e) => {
                set_error(
                    error_out,
                    ChromaErrorCode::InvalidArgument,
                    "Failed to convert IDs array",
                    func_name,
                    Some(&e.to_string()),
                );
                return ChromaErrorCode::InvalidArgument as c_int;
            }
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
                    set_error(
                        error_out,
                        ChromaErrorCode::InvalidArgument,
                        "Embedding pointer is null",
                        func_name,
                        Some(&format!("Null embedding at index {}", i)),
                    );
                    return ChromaErrorCode::InvalidArgument as c_int;
                }
            }
        }
        Some(result)
    } else {
        None
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
                        Err(e) => {
                            set_error(
                                error_out,
                                ChromaErrorCode::InvalidArgument,
                                "Failed to convert metadata string",
                                func_name,
                                Some(&format!("Error at index {}: {}", i, e)),
                            );
                            return ChromaErrorCode::InvalidArgument as c_int;
                        }
                    };

                    if metadata_str.is_empty() {
                        result.push(None);
                    } else {
                        match serde_json::from_str::<UpdateMetadata>(&metadata_str) {
                            Ok(metadata) => result.push(Some(metadata)),
                            Err(e) => {
                                set_error(
                                    error_out,
                                    ChromaErrorCode::ValidationError,
                                    "Invalid metadata JSON",
                                    func_name,
                                    Some(&format!("Error parsing metadata at index {}: {}", i, e)),
                                );
                                return ChromaErrorCode::ValidationError as c_int;
                            }
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
                        }
                        Err(e) => {
                            set_error(
                                error_out,
                                ChromaErrorCode::InvalidArgument,
                                "Failed to convert document string",
                                func_name,
                                Some(&format!("Error at index {}: {}", i, e)),
                            );
                            return ChromaErrorCode::InvalidArgument as c_int;
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

    // Parse collection ID
    let collection_id = match uuid::Uuid::parse_str(&collection.id) {
        Ok(id) => CollectionUuid(id),
        Err(e) => {
            set_error(
                error_out,
                ChromaErrorCode::InvalidUuid,
                "Invalid collection UUID",
                func_name,
                Some(&format!("UUID parse error: {}", e)),
            );
            return ChromaErrorCode::InvalidUuid as c_int;
        }
    };

    // Create upsert request
    let request = match UpsertCollectionRecordsRequest::try_new(
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
        Err(e) => {
            set_error(
                error_out,
                ChromaErrorCode::ValidationError,
                "Failed to create upsert request",
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
        .block_on(async { frontend.upsert(request).await })
    {
        Ok(_) => {
            set_success(error_out);
            ChromaErrorCode::Success as c_int
        }
        Err(e) => {
            set_error(
                error_out,
                ChromaErrorCode::InternalError,
                "Failed to upsert documents",
                func_name,
                Some(&format!("Error: {:?}", e)),
            );
            ChromaErrorCode::InternalError as c_int
        }
    }
}

/// Deletes documents from a collection
#[no_mangle]
pub extern "C" fn chroma_delete(
    client_handle: *mut ChromaClient,
    collection_handle: *const ChromaCollection,
    ids: *const *const c_char,
    ids_count: size_t,
    where_filter_json: *const c_char,
    where_document_filter: *const c_char,
    error_out: *mut *mut ChromaError,
) -> c_int {
    let func_name = "chroma_delete";

    if client_handle.is_null() || collection_handle.is_null() {
        let message = if client_handle.is_null() {
            "Client handle pointer is null"
        } else {
            "Collection handle pointer is null"
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

    // If both ids and where filter are null, return an error
    if ids.is_null() && where_filter_json.is_null() && where_document_filter.is_null() {
        set_error(
            error_out,
            ChromaErrorCode::InvalidArgument,
            "Either document IDs or filter criteria must be specified",
            func_name,
            None,
        );
        return ChromaErrorCode::InvalidArgument as c_int;
    }

    let client = unsafe { &mut *client_handle };
    let collection = unsafe { &*collection_handle };

    // Convert C string array to Rust vector
    let ids_vec = if !ids.is_null() && ids_count > 0 {
        unsafe {
            match c_array_to_vec_string(ids, ids_count) {
                Ok(v) => Some(v),
                Err(e) => {
                    set_error(
                        error_out,
                        ChromaErrorCode::InvalidArgument,
                        "Failed to convert IDs array",
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

    // Parse where filters
    let where_filter = unsafe {
        let where_json_str = if !where_filter_json.is_null() {
            match c_str_to_string(where_filter_json) {
                Ok(s) => Some(s),
                Err(e) => {
                    set_error(
                        error_out,
                        ChromaErrorCode::InvalidArgument,
                        "Failed to convert where filter JSON string",
                        func_name,
                        Some(&e.to_string()),
                    );
                    return ChromaErrorCode::InvalidArgument as c_int;
                }
            }
        } else {
            None
        };

        let where_document = if !where_document_filter.is_null() {
            match c_str_to_string(where_document_filter) {
                Ok(s) => Some(s),
                Err(e) => {
                    set_error(
                        error_out,
                        ChromaErrorCode::InvalidArgument,
                        "Failed to convert document filter string",
                        func_name,
                        Some(&e.to_string()),
                    );
                    return ChromaErrorCode::InvalidArgument as c_int;
                }
            }
        } else {
            None
        };

        // Only attempt to parse where filters if they're actually provided
        if where_json_str.is_some() || where_document.is_some() {
            match RawWhereFields::from_json_str(
                where_json_str.as_deref(),
                where_document.as_deref(),
            ) {
                Ok(raw) => match raw.parse() {
                    Ok(parsed) => parsed,
                    Err(e) => {
                        set_error(
                            error_out,
                            ChromaErrorCode::ValidationError,
                            "Failed to parse where filters",
                            func_name,
                            Some(&format!("Filter validation error: {:?}", e)),
                        );
                        return ChromaErrorCode::ValidationError as c_int;
                    }
                },
                Err(e) => {
                    set_error(
                        error_out,
                        ChromaErrorCode::ValidationError,
                        "Failed to create where filters",
                        func_name,
                        Some(&format!("Filter creation error: {:?}", e)),
                    );
                    return ChromaErrorCode::ValidationError as c_int;
                }
            }
        } else {
            // No where filter provided, use None as default
            None
        }
    };

    // Parse collection ID
    let collection_id = match uuid::Uuid::parse_str(&collection.id) {
        Ok(id) => CollectionUuid(id),
        Err(e) => {
            set_error(
                error_out,
                ChromaErrorCode::InvalidUuid,
                "Invalid collection UUID",
                func_name,
                Some(&format!("UUID parse error: {}", e)),
            );
            return ChromaErrorCode::InvalidUuid as c_int;
        }
    };

    // Create delete request
    let request = match DeleteCollectionRecordsRequest::try_new(
        collection.tenant.clone(),
        collection.database.clone(),
        collection_id,
        ids_vec,
        where_filter,
    ) {
        Ok(req) => req,
        Err(e) => {
            set_error(
                error_out,
                ChromaErrorCode::ValidationError,
                "Failed to create delete request",
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
        .block_on(async { frontend.delete(request).await })
    {
        Ok(_) => {
            set_success(error_out);
            ChromaErrorCode::Success as c_int
        }
        Err(e) => {
            set_error(
                error_out,
                ChromaErrorCode::InternalError,
                "Failed to delete documents",
                func_name,
                Some(&format!("Error: {:?}", e)),
            );
            ChromaErrorCode::InternalError as c_int
        }
    }
}

/// Gets documents from a collection
#[no_mangle]
pub extern "C" fn chroma_get(
    client_handle: *mut ChromaClient,
    collection_handle: *const ChromaCollection,
    ids: *const *const c_char,
    ids_count: size_t,
    where_filter_json: *const c_char,
    where_document_filter: *const c_char,
    limit: c_uint,
    offset: c_uint,
    include_embeddings: bool,
    include_metadatas: bool,
    include_documents: bool,
    result: *mut *mut ChromaQueryResult,
    error_out: *mut *mut ChromaError,
) -> c_int {
    let func_name = "chroma_get";

    if client_handle.is_null() || collection_handle.is_null() || result.is_null() {
        let message = if client_handle.is_null() {
            "Client handle pointer is null"
        } else if collection_handle.is_null() {
            "Collection handle pointer is null"
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

    let client = unsafe { &mut *client_handle };
    let collection = unsafe { &*collection_handle };

    // Convert C string array to Rust vector
    let ids_vec = if !ids.is_null() && ids_count > 0 {
        unsafe {
            match c_array_to_vec_string(ids, ids_count) {
                Ok(v) => Some(v),
                Err(e) => {
                    set_error(
                        error_out,
                        ChromaErrorCode::InvalidArgument,
                        "Failed to convert IDs array",
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

    // Parse where filters
    let where_filter = unsafe {
        let where_json_str = if !where_filter_json.is_null() {
            match c_str_to_string(where_filter_json) {
                Ok(s) => Some(s),
                Err(e) => {
                    set_error(
                        error_out,
                        ChromaErrorCode::InvalidArgument,
                        "Failed to convert where filter JSON string",
                        func_name,
                        Some(&e.to_string()),
                    );
                    return ChromaErrorCode::InvalidArgument as c_int;
                }
            }
        } else {
            None
        };

        let where_document = if !where_document_filter.is_null() {
            match c_str_to_string(where_document_filter) {
                Ok(s) => Some(s),
                Err(e) => {
                    set_error(
                        error_out,
                        ChromaErrorCode::InvalidArgument,
                        "Failed to convert document filter string",
                        func_name,
                        Some(&e.to_string()),
                    );
                    return ChromaErrorCode::InvalidArgument as c_int;
                }
            }
        } else {
            None
        };

        // Only attempt to parse where filters if they're actually provided
        if where_json_str.is_some() || where_document.is_some() {
            match RawWhereFields::from_json_str(
                where_json_str.as_deref(),
                where_document.as_deref(),
            ) {
                Ok(raw) => match raw.parse() {
                    Ok(parsed) => Some(parsed),
                    Err(e) => {
                        set_error(
                            error_out,
                            ChromaErrorCode::ValidationError,
                            "Failed to parse where filters",
                            func_name,
                            Some(&format!("Filter validation error: {:?}", e)),
                        );
                        return ChromaErrorCode::ValidationError as c_int;
                    }
                },
                Err(e) => {
                    set_error(
                        error_out,
                        ChromaErrorCode::ValidationError,
                        "Failed to create where filters",
                        func_name,
                        Some(&format!("Filter creation error: {:?}", e)),
                    );
                    return ChromaErrorCode::ValidationError as c_int;
                }
            }
        } else {
            // No where filter provided, use None as default
            None
        }
    };

    // Parse collection ID
    let collection_id = match uuid::Uuid::parse_str(&collection.id) {
        Ok(id) => CollectionUuid(id),
        Err(e) => {
            set_error(
                error_out,
                ChromaErrorCode::InvalidUuid,
                "Invalid collection UUID",
                func_name,
                Some(&format!("UUID parse error: {}", e)),
            );
            return ChromaErrorCode::InvalidUuid as c_int;
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

    let include_list = match IncludeList::try_from(include) {
        Ok(list) => list,
        Err(e) => {
            set_error(
                error_out,
                ChromaErrorCode::ValidationError,
                "Invalid include list",
                func_name,
                Some(&format!("Include list validation error: {:?}", e)),
            );
            return ChromaErrorCode::ValidationError as c_int;
        }
    };

    // Create get request
    let request = match GetRequest::try_new(
        collection.tenant.clone(),
        collection.database.clone(),
        collection_id,
        ids_vec,
        where_filter.flatten(), // Flatten Option<Option<Where>> to Option<Where>
        if limit > 0 { Some(limit) } else { None },
        offset,
        include_list,
    ) {
        Ok(req) => req,
        Err(e) => {
            set_error(
                error_out,
                ChromaErrorCode::ValidationError,
                "Failed to create get request",
                func_name,
                Some(&format!("Validation error: {:?}", e)),
            );
            return ChromaErrorCode::ValidationError as c_int;
        }
    };

    // Execute get
    let mut frontend = client.frontend.clone();
    let get_response = match client
        .runtime
        .block_on(async { frontend.get(request).await })
    {
        Ok(resp) => resp,
        Err(e) => {
            set_error(
                error_out,
                ChromaErrorCode::InternalError,
                "Failed to get documents",
                func_name,
                Some(&format!("Error: {:?}", e)),
            );
            return ChromaErrorCode::InternalError as c_int;
        }
    };

    // Prepare result structure
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
    if !get_response.ids.is_empty() {
        let (array, count) = vec_string_to_c_array(get_response.ids);
        query_result.ids = array;
        query_result.ids_count = count;
    }

    // Set metadata if available
    if let Some(metadatas) = get_response.metadatas {
        if !metadatas.is_empty() {
            let metadata_strings: Vec<String> = metadatas
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
    if let Some(documents) = get_response.documents {
        if !documents.is_empty() {
            let doc_strings: Vec<String> = documents
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

    set_success(error_out);
    ChromaErrorCode::Success as c_int
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
    error_out: *mut *mut ChromaError,
) -> c_int {
    let func_name = "chroma_query";

    if client_handle.is_null()
        || collection_handle.is_null()
        || query_embeddings.is_null()
        || result.is_null()
    {
        let message = if client_handle.is_null() {
            "Client handle pointer is null"
        } else if collection_handle.is_null() {
            "Collection handle pointer is null"
        } else if query_embeddings.is_null() {
            "Query embeddings pointer is null"
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

    let client = unsafe { &mut *client_handle };
    let collection = unsafe { &*collection_handle };

    // Parse collection ID
    let collection_id = match uuid::Uuid::parse_str(&collection.id) {
        Ok(id) => CollectionUuid(id),
        Err(e) => {
            set_error(
                error_out,
                ChromaErrorCode::InvalidUuid,
                "Invalid collection UUID",
                func_name,
                Some(&format!("UUID parse error: {}", e)),
            );
            return ChromaErrorCode::InvalidUuid as c_int;
        }
    };

    // Convert C embedding to Rust vector
    let query_embedding_vec = unsafe { vec![c_array_to_vec_f32(query_embeddings, embedding_dim)] };
    if query_embedding_vec[0].is_empty() || query_embedding_vec[0].len() != embedding_dim {
        set_error(
            error_out,
            ChromaErrorCode::InvalidArgument,
            "Invalid query embedding",
            func_name,
            Some(&format!(
                "Expected dimension {}, got {}",
                embedding_dim,
                query_embedding_vec[0].len()
            )),
        );
        return ChromaErrorCode::InvalidArgument as c_int;
    }

    // Parse where filters
    let where_filter = unsafe {
        let where_json_str = if !where_filter_json.is_null() {
            match c_str_to_string(where_filter_json) {
                Ok(s) => Some(s),
                Err(e) => {
                    set_error(
                        error_out,
                        ChromaErrorCode::InvalidArgument,
                        "Failed to convert where filter JSON string",
                        func_name,
                        Some(&e.to_string()),
                    );
                    return ChromaErrorCode::InvalidArgument as c_int;
                }
            }
        } else {
            None
        };

        let where_document = if !where_document_filter.is_null() {
            match c_str_to_string(where_document_filter) {
                Ok(s) => Some(s),
                Err(e) => {
                    set_error(
                        error_out,
                        ChromaErrorCode::InvalidArgument,
                        "Failed to convert document filter string",
                        func_name,
                        Some(&e.to_string()),
                    );
                    return ChromaErrorCode::InvalidArgument as c_int;
                }
            }
        } else {
            None
        };

        // Only attempt to parse where filters if they're actually provided
        if where_json_str.is_some() || where_document.is_some() {
            match RawWhereFields::from_json_str(
                where_json_str.as_deref(),
                where_document.as_deref(),
            ) {
                Ok(raw) => match raw.parse() {
                    Ok(parsed) => parsed,
                    Err(e) => {
                        set_error(
                            error_out,
                            ChromaErrorCode::ValidationError,
                            "Failed to parse where filters",
                            func_name,
                            Some(&format!("Filter validation error: {:?}", e)),
                        );
                        return ChromaErrorCode::ValidationError as c_int;
                    }
                },
                Err(e) => {
                    set_error(
                        error_out,
                        ChromaErrorCode::ValidationError,
                        "Failed to create where filters",
                        func_name,
                        Some(&format!("Filter creation error: {:?}", e)),
                    );
                    return ChromaErrorCode::ValidationError as c_int;
                }
            }
        } else {
            // No where filter provided, use default None
            None
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
        Err(e) => {
            set_error(
                error_out,
                ChromaErrorCode::ValidationError,
                "Invalid include list",
                func_name,
                Some(&format!("Include list validation error: {:?}", e)),
            );
            return ChromaErrorCode::ValidationError as c_int;
        }
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
        Err(e) => {
            set_error(
                error_out,
                ChromaErrorCode::ValidationError,
                "Failed to create query request",
                func_name,
                Some(&format!("Validation error: {:?}", e)),
            );
            return ChromaErrorCode::ValidationError as c_int;
        }
    };

    // Execute query
    let mut frontend = client.frontend.clone();
    let query_response = match client
        .runtime
        .block_on(async { frontend.query(request).await })
    {
        Ok(resp) => resp,
        Err(e) => {
            set_error(
                error_out,
                ChromaErrorCode::InternalError,
                "Failed to execute query",
                func_name,
                Some(&format!("Error: {:?}", e)),
            );
            return ChromaErrorCode::InternalError as c_int;
        }
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
        if !distances.is_empty() && !distances[0].is_empty() {
            let distance_vec: Vec<f32> = distances[0].iter().map(|d| d.unwrap_or(0.0)).collect();

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

    set_success(error_out);
    ChromaErrorCode::Success as c_int
}
