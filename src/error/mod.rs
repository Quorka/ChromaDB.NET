// Error handling module for ChromaDB C# bindings
use libc::c_char;
use std::ffi::CString;
use std::ptr;

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

/// Detailed error information for ChromaDB C API
#[repr(C)]
pub struct ChromaError {
    /// Error code
    pub code: ChromaErrorCode,
    /// Error message
    pub message: *mut c_char,
    /// Source of the error (e.g., function name)
    pub source: *mut c_char,
    /// Details about the error (additional context)
    pub details: *mut c_char,
}

impl ChromaError {
    /// Creates a new error object
    pub fn new(code: ChromaErrorCode, message: &str, source: &str, details: Option<&str>) -> Self {
        ChromaError {
            code,
            message: crate::utils::string_to_c_str(message.to_string()),
            source: crate::utils::string_to_c_str(source.to_string()),
            details: match details {
                Some(d) => crate::utils::string_to_c_str(d.to_string()),
                None => ptr::null_mut(),
            },
        }
    }

    /// Creates a success result
    pub fn success() -> Self {
        ChromaError {
            code: ChromaErrorCode::Success,
            message: ptr::null_mut(),
            source: ptr::null_mut(),
            details: ptr::null_mut(),
        }
    }
}

/// Helper function to set error out parameter
pub fn set_error(
    error_out: *mut *mut ChromaError,
    code: ChromaErrorCode,
    message: &str,
    source: &str,
    details: Option<&str>,
) {
    if !error_out.is_null() {
        let error = Box::new(ChromaError::new(code, message, source, details));
        unsafe {
            *error_out = Box::into_raw(error);
        }
    }
}

/// Helper function to set success result
pub fn set_success(error_out: *mut *mut ChromaError) {
    if !error_out.is_null() {
        let error = Box::new(ChromaError::success());
        unsafe {
            *error_out = Box::into_raw(error);
        }
    }
}

/// Frees memory allocated for ChromaError
#[no_mangle]
pub extern "C" fn chroma_free_error(error: *mut ChromaError) {
    if !error.is_null() {
        unsafe {
            let error = &mut *error;

            if !error.message.is_null() {
                let _ = CString::from_raw(error.message);
                error.message = ptr::null_mut();
            }

            if !error.source.is_null() {
                let _ = CString::from_raw(error.source);
                error.source = ptr::null_mut();
            }

            if !error.details.is_null() {
                let _ = CString::from_raw(error.details);
                error.details = ptr::null_mut();
            }

            libc::free(error as *mut ChromaError as *mut libc::c_void);
        }
    }
}
