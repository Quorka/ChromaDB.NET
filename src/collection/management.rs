// Collection management functions for ChromaDB C# bindings
use chroma_types::{
    CollectionConfiguration, CreateCollectionRequest, GetCollectionRequest,
    InternalCollectionConfiguration, Metadata,
};
use libc::{c_char, c_int};

use crate::client::ChromaClient;
use crate::collection::types::ChromaCollection;
use crate::error::{set_error, set_success, ChromaError, ChromaErrorCode};
use crate::utils::{c_str_to_string, DEFAULT_DATABASE, DEFAULT_TENANT};

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
    error_out: *mut *mut ChromaError,
) -> c_int {
    let func_name = "chroma_create_collection";

    // Check required arguments
    if client_handle.is_null() || name_ptr.is_null() || collection_handle.is_null() {
        let message = if client_handle.is_null() {
            "Client handle pointer is null"
        } else if name_ptr.is_null() {
            "Collection name pointer is null"
        } else {
            "Collection handle output pointer is null"
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

    // Parse collection name
    let name = unsafe {
        match c_str_to_string(name_ptr) {
            Ok(s) => s,
            Err(e) => {
                set_error(
                    error_out,
                    ChromaErrorCode::InvalidArgument,
                    "Invalid collection name",
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

    // Parse database name
    let database = if !database_ptr.is_null() {
        unsafe {
            match c_str_to_string(database_ptr) {
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
        }
    } else {
        DEFAULT_DATABASE.to_string()
    };

    // Parse configuration JSON if provided
    let configuration_json = if !config_json_ptr.is_null() {
        unsafe {
            let config_json_str = match c_str_to_string(config_json_ptr) {
                Ok(s) => s,
                Err(e) => {
                    set_error(
                        error_out,
                        ChromaErrorCode::InvalidArgument,
                        "Invalid configuration JSON",
                        func_name,
                        Some(&e.to_string()),
                    );
                    return ChromaErrorCode::InvalidArgument as c_int;
                }
            };

            match serde_json::from_str::<CollectionConfiguration>(&config_json_str) {
                Ok(config) => Some(config),
                Err(e) => {
                    set_error(
                        error_out,
                        ChromaErrorCode::ValidationError,
                        "Failed to parse configuration JSON",
                        func_name,
                        Some(&e.to_string()),
                    );
                    return ChromaErrorCode::ValidationError as c_int;
                }
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
                Err(e) => {
                    set_error(
                        error_out,
                        ChromaErrorCode::InvalidArgument,
                        "Invalid metadata JSON",
                        func_name,
                        Some(&e.to_string()),
                    );
                    return ChromaErrorCode::InvalidArgument as c_int;
                }
            };

            match serde_json::from_str::<Metadata>(&metadata_json_str) {
                Ok(metadata) => Some(metadata),
                Err(e) => {
                    set_error(
                        error_out,
                        ChromaErrorCode::ValidationError,
                        "Failed to parse metadata JSON",
                        func_name,
                        Some(&e.to_string()),
                    );
                    return ChromaErrorCode::ValidationError as c_int;
                }
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
                None,
                ) {
                Ok(config) => Some(config),
                Err(e) => {
                    set_error(
                        error_out,
                        ChromaErrorCode::ValidationError,
                        "Invalid collection configuration",
                        func_name,
                        Some(&format!("Configuration validation error: {:?}", e)),
                    );
                    return ChromaErrorCode::ValidationError as c_int;
                }
            }
        }
        None => None,
    };

    // Create the collection request
    let request = match CreateCollectionRequest::try_new(
        tenant.clone(),
        database.clone(),
        name,
        metadata,
        configuration,
        get_or_create,
    ) {
        Ok(req) => req,
        Err(e) => {
            set_error(
                error_out,
                ChromaErrorCode::ValidationError,
                "Failed to create collection request",
                func_name,
                Some(&format!("Validation error: {:?}", e)),
            );
            return ChromaErrorCode::ValidationError as c_int;
        }
    };

    // Execute the request
    let mut frontend = client.frontend.clone();

    match client
        .runtime
        .block_on(async { frontend.create_collection(request).await })
    {
        Ok(collection) => {
            // Create the collection wrapper
            let collection_wrapper = Box::new(ChromaCollection {
                id: collection.collection_id.0.to_string(),
                tenant,
                database,
            });

            // Set the output handle
            unsafe {
                *collection_handle = Box::into_raw(collection_wrapper);
            }

            // Return success
            set_success(error_out);
            ChromaErrorCode::Success as c_int
        }
        Err(e) => {
            set_error(
                error_out,
                ChromaErrorCode::InternalError,
                "Failed to create collection",
                func_name,
                Some(&format!("Error: {:?}", e)),
            );
            ChromaErrorCode::InternalError as c_int
        }
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
    error_out: *mut *mut ChromaError,
) -> c_int {
    let func_name = "chroma_get_collection";

    if client_handle.is_null() || name_ptr.is_null() || collection_handle.is_null() {
        let message = if client_handle.is_null() {
            "Client handle pointer is null"
        } else if name_ptr.is_null() {
            "Collection name pointer is null"
        } else {
            "Collection handle output pointer is null"
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
                    "Invalid collection name",
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

    let database = if !database_ptr.is_null() {
        unsafe {
            match c_str_to_string(database_ptr) {
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
        }
    } else {
        DEFAULT_DATABASE.to_string()
    };

    let client = unsafe { &mut *client_handle };

    let request = match GetCollectionRequest::try_new(tenant.clone(), database.clone(), name) {
        Ok(req) => req,
        Err(e) => {
            set_error(
                error_out,
                ChromaErrorCode::ValidationError,
                "Failed to create get collection request",
                func_name,
                Some(&format!("Validation error: {:?}", e)),
            );
            return ChromaErrorCode::ValidationError as c_int;
        }
    };

    let mut frontend = client.frontend.clone();

    match client
        .runtime
        .block_on(async { frontend.get_collection(request).await })
    {
        Ok(collection) => {
            let collection_wrapper = Box::new(ChromaCollection {
                id: collection.collection_id.0.to_string(),
                tenant,
                database,
            });

            unsafe {
                *collection_handle = Box::into_raw(collection_wrapper);
            }

            set_success(error_out);
            ChromaErrorCode::Success as c_int
        }
        Err(e) => {
            set_error(
                error_out,
                ChromaErrorCode::NotFound,
                "Collection not found",
                func_name,
                Some(&format!("Error: {:?}", e)),
            );
            ChromaErrorCode::NotFound as c_int
        }
    }
}
