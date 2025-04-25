// Utility functions for ChromaDB C# bindings
use anyhow::{anyhow, Result};
use libc::{c_char, c_float, size_t};
use std::ffi::{CStr, CString};
use std::ptr;

/// Constants
pub const DEFAULT_DATABASE: &str = "default_database";
pub const DEFAULT_TENANT: &str = "default_tenant";

/// Converts a C string to a Rust string
pub unsafe fn c_str_to_string(s: *const c_char) -> Result<String> {
    if s.is_null() {
        return Err(anyhow!("Null string pointer"));
    }

    Ok(CStr::from_ptr(s).to_string_lossy().into_owned())
}

/// Converts a Rust string to a C string
pub fn string_to_c_str(s: String) -> *mut c_char {
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

/// Converts a Rust vector of strings to a C string array
pub fn vec_string_to_c_array(strings: Vec<String>) -> (*mut *mut c_char, size_t) {
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
pub unsafe fn c_array_to_vec_string(
    array: *const *const c_char,
    count: size_t,
) -> Result<Vec<String>> {
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
pub unsafe fn c_array_to_vec_f32(array: *const c_float, count: size_t) -> Vec<f32> {
    if array.is_null() {
        return Vec::new();
    }

    let slice = std::slice::from_raw_parts(array, count);
    slice.to_vec()
}

/// Converts a Rust vector of f32 values to a C float array
pub fn vec_f32_to_c_array(values: Vec<f32>) -> (*mut c_float, size_t) {
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
