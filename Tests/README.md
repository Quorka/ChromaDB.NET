# ChromaDB.NET Tests

This directory contains unit and integration tests for the ChromaDB.NET library.

## Test Structure

The test suite is organized into several test files:

- **ChromaClientTests.cs**: Basic tests for the ChromaClient class
- **IdiomsApiTests.cs**: Tests for the idiomatic C# API additions
- **WhereFilterTests.cs**: Tests for the WhereFilter fluent API
- **DatabaseOperationsTests.cs**: Tests for database-related operations
- **IntegrationTests.cs**: End-to-end tests covering full workflows

## Running Tests

### Prerequisites

1. Ensure you have the .NET 6.0 SDK installed
2. Build the native libraries by running the build script in the root directory:
   - On Linux/macOS: `./build.sh`
   - On Windows: `./build.ps1`

### Run All Tests

From the test directory:

```bash
dotnet test
```

Or from the project root:

```bash
dotnet test Tests/ChromaDB.NET.Tests/ChromaDB.NET.Tests.csproj
```

### Run Specific Tests

To run a specific test category:

```bash
dotnet test --filter "ClassName=ChromaDB.NET.Tests.IdiomsApiTests"
```

To run a single test:

```bash
dotnet test --filter "FullyQualifiedName=ChromaDB.NET.Tests.IntegrationTests.FullWorkflow_Success"
```

## Troubleshooting

If you encounter issues with the tests:

1. **Native Library Loading Errors**: Ensure the native libraries (.dll, .so, .dylib) are properly built and copied to the output directory. The project includes a build step that should copy these files automatically.

2. **Permission Issues**: On Linux, you may need to make the library executable:
   ```bash
   chmod +x Tests/ChromaDB.NET.Tests/bin/Debug/net6.0/runtimes/linux-x64/native/libchroma_csharp.so
   ```

3. **Runtime Errors**: Try running the tests in Debug mode for better error information:
   ```bash
   dotnet test -c Debug
   ```

## Writing New Tests

When adding new tests, follow these guidelines:

1. **Test Isolation**: Each test should be independent and not rely on state from other tests
2. **Test Cleanup**: Use the TestCleanup method to clean up any resources created during tests
3. **Test Coverage**: Aim to test both happy paths and error conditions
4. **Test Names**: Use descriptive names in the format of `MethodName_Condition_ExpectedResult`

Example:
```csharp
[TestMethod]
public void GetCollection_NonExistentCollection_ThrowsException()
{
    // Test implementation
}
```