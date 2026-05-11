use libc::c_char;
use std::ffi::CString;
use std::ptr;

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

#[repr(C)]
pub struct ChromaError {
    pub code: ChromaErrorCode,
    pub message: *mut c_char,
    pub source: *mut c_char,
    pub details: *mut c_char,
}

impl ChromaError {
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
}

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

pub fn set_success(error_out: *mut *mut ChromaError) {
    if !error_out.is_null() {
        unsafe {
            *error_out = ptr::null_mut();
        }
    }
}

#[no_mangle]
pub extern "C" fn chroma_free_error(error: *mut ChromaError) {
    if !error.is_null() {
        unsafe {
            let error = Box::from_raw(error);

            if !error.message.is_null() {
                let _ = CString::from_raw(error.message);
            }

            if !error.source.is_null() {
                let _ = CString::from_raw(error.source);
            }

            if !error.details.is_null() {
                let _ = CString::from_raw(error.details);
            }
        }
    }
}
