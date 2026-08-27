# Quality Gates

Before declaring any engineering task complete, AI2 must execute and verify the following quality gates:

1. **Solution Build**:
   ```bash
   dotnet build SimsConverter.sln
   ```
   Must succeed with 0 errors.

2. **Automated Unit Tests**:
   ```bash
   dotnet test SimsConverter.sln
   ```
   All tests in `Domain.Tests` and `Package.Tests` must pass without failure.

3. **No Unapproved Commits**:
   - Do not execute `git commit` or `git push` without explicit user permission.
