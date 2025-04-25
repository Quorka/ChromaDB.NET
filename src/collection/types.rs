// Type definitions for ChromaDB collection

/// Collection handle for ChromaDB
#[repr(C)]
pub struct ChromaCollection {
    pub(crate) id: String,
    pub(crate) tenant: String,
    pub(crate) database: String,
}

/// Frees memory allocated for ChromaCollection
#[no_mangle]
pub extern "C" fn chroma_destroy_collection(collection_handle: *mut ChromaCollection) -> i32 {
    use crate::error::ChromaErrorCode;

    if !collection_handle.is_null() {
        unsafe {
            let _ = Box::from_raw(collection_handle);
        }
        ChromaErrorCode::Success as i32
    } else {
        ChromaErrorCode::InvalidArgument as i32
    }
}
