// Define module structure for C# bindings
#![deny(clippy::all)]

pub mod client;
pub mod collection;
pub mod error;
pub mod types;
pub mod utils;

// Re-export main components for API users
pub use client::*;
pub use collection::*;
pub use error::*;
pub use types::*;
