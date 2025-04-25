// Type definitions for ChromaDB C# bindings
use libc::{c_char, c_float, c_int, size_t};

/// Configuration for SQLite database
#[repr(C)]
pub struct SqliteConfigFFI {
    pub url: *const c_char,
    pub hash_type: c_int,
    pub migration_mode: c_int,
}

/// Response from query operations
#[repr(C)]
pub struct ChromaQueryResult {
    pub ids: *mut *mut c_char,
    pub ids_count: size_t,
    pub distances: *mut c_float,
    pub distances_count: size_t,
    pub metadata_json: *mut *mut c_char,
    pub metadata_count: size_t,
    pub documents: *mut *mut c_char,
    pub documents_count: size_t,
}

/// Embedding vector
#[repr(C)]
pub struct ChromaEmbedding {
    pub values: *const c_float,
    pub dimension: size_t,
}

/// Result set information for ChromaDB operations
#[repr(C)]
pub struct ChromaResultSet {
    pub ids: *mut *mut c_char,
    pub count: size_t,
}

/// Frees memory allocated for ChromaQueryResult
#[no_mangle]
pub extern "C" fn chroma_free_query_result(result: *mut ChromaQueryResult) {
    if !result.is_null() {
        unsafe {
            let result = &mut *result;

            crate::utils::chroma_free_string_array(result.ids, result.ids_count);

            if !result.distances.is_null() {
                libc::free(result.distances as *mut libc::c_void);
            }

            crate::utils::chroma_free_string_array(result.metadata_json, result.metadata_count);
            crate::utils::chroma_free_string_array(result.documents, result.documents_count);

            libc::free(result as *mut ChromaQueryResult as *mut libc::c_void);
        }
    }
}
