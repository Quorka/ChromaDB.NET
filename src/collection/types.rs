use libc::c_int;

use crate::error::{set_error, set_success, ChromaError, ChromaErrorCode};

pub struct ChromaCollection {
    pub(crate) id: String,
    pub(crate) tenant: String,
    pub(crate) database: String,
}

#[no_mangle]
pub extern "C" fn chroma_destroy_collection(
    collection_handle: *mut ChromaCollection,
    error_out: *mut *mut ChromaError,
) -> c_int {
    if collection_handle.is_null() {
        set_error(
            error_out,
            ChromaErrorCode::InvalidArgument,
            "Collection handle pointer is null",
            "chroma_destroy_collection",
            None,
        );
        return ChromaErrorCode::InvalidArgument as c_int;
    }

    unsafe {
        let _ = Box::from_raw(collection_handle);
    }

    set_success(error_out);
    ChromaErrorCode::Success as c_int
}
