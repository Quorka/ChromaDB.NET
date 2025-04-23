#ifndef CHROMA_API_H
#define CHROMA_API_H

#include <stddef.h>
#include <stdint.h>

#ifdef __cplusplus
extern "C" {
#endif

// Error codes
typedef enum ChromaErrorCode {
    CHROMA_SUCCESS = 0,
    CHROMA_INVALID_ARGUMENT = 1,
    CHROMA_INTERNAL_ERROR = 2,
    CHROMA_MEMORY_ERROR = 3,
    CHROMA_NOT_FOUND = 4,
    CHROMA_VALIDATION_ERROR = 5,
    CHROMA_INVALID_UUID = 6,
    CHROMA_NOT_IMPLEMENTED = 7
} ChromaErrorCode;

// Opaque handle types
typedef struct ChromaClient ChromaClient;
typedef struct ChromaCollection ChromaCollection;

// SQLite configuration
typedef struct SqliteConfigFFI {
    const char* url;
    int hash_type;
    int migration_mode;
} SqliteConfigFFI;

// Query result structure
typedef struct ChromaQueryResult {
    char** ids;
    size_t ids_count;
    float* distances;
    size_t distances_count;
    char** metadata_json;
    size_t metadata_count;
    char** documents;
    size_t documents_count;
} ChromaQueryResult;

// Result set information
typedef struct ChromaResultSet {
    char** ids;
    size_t count;
} ChromaResultSet;

// Memory management functions
int chroma_free_string(char* str);
int chroma_free_string_array(char** array, size_t count);
int chroma_free_query_result(ChromaQueryResult* result);

// Client management
int chroma_create_client(
    int allow_reset,
    const SqliteConfigFFI* sqlite_config,
    size_t hnsw_cache_size,
    const char* persist_path,
    ChromaClient** client_handle
);

int chroma_destroy_client(ChromaClient* client_handle);
int chroma_heartbeat(ChromaClient* client_handle, uint64_t* result);

// Database management
int chroma_create_database(
    ChromaClient* client_handle,
    const char* name,
    const char* tenant
);

int chroma_get_database(
    ChromaClient* client_handle,
    const char* name,
    const char* tenant,
    char** id_result
);

int chroma_delete_database(
    ChromaClient* client_handle,
    const char* name,
    const char* tenant
);

// Collection management
int chroma_create_collection(
    ChromaClient* client_handle,
    const char* name,
    const char* config_json,
    const char* metadata_json,
    int get_or_create,
    const char* tenant,
    const char* database,
    ChromaCollection** collection_handle
);

int chroma_get_collection(
    ChromaClient* client_handle,
    const char* name,
    const char* tenant,
    const char* database,
    ChromaCollection** collection_handle
);

int chroma_destroy_collection(ChromaCollection* collection_handle);

// Document management
int chroma_add(
    ChromaClient* client_handle,
    const ChromaCollection* collection_handle,
    const char** ids,
    size_t ids_count,
    const float** embeddings,
    size_t embedding_dim,
    const char** metadatas_json,
    const char** documents
);

int chroma_query(
    ChromaClient* client_handle,
    const ChromaCollection* collection_handle,
    const float* query_embedding,
    size_t embedding_dim,
    unsigned int n_results,
    const char* where_filter_json,
    const char* where_document_filter,
    int include_embeddings,
    int include_metadatas,
    int include_documents,
    int include_distances,
    ChromaQueryResult** result
);

#ifdef __cplusplus
}
#endif

#endif // CHROMA_API_H