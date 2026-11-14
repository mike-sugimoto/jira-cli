- source_spec: `_bmad-output/implementation-artifacts/spec-1-1-solution-skeleton-with-architecture-guards.md`
  summary: Pin that CompositionRoot's ValidateOnBuild/ValidateScopes actually reject a bad registration once the first real service is registered (Story 1.2).
  evidence: Every Add…() registers nothing in 1.1, so removing either option or an Add…() call from CompositionRoot.Build leaves every test green (verification-gap review, pass 1).
  status: resolved by Story 1.2 — `CompositionRootTests` uses the `CompositionRoot.Build(ConfigLoadResult, Action<IServiceCollection>?)` seam to prove a missing dependency fails at build (ValidateOnBuild) and a scoped service fails from the root (ValidateScopes).
- source_spec: `_bmad-output/implementation-artifacts/spec-1-2-fixed-config-location-and-config-loading.md`
  summary: Run the Linux path tests (native passwd lookup, hostile environment) in a routine verification path and assert the home equals the passwd entry.
  evidence: Linux-only tests skip on Windows and there is no CI; a wrong `struct passwd` field or broken ERANGE retry would stay green. Managed `SetEnvironmentVariable` does not reach libc `getenv`, so the Linux hostile-env test is weak.
- source_spec: `_bmad-output/implementation-artifacts/spec-1-2-fixed-config-location-and-config-loading.md`
  summary: A missing native library (musl/Alpine `libc`, Nano Server shell32) may throw DllNotFoundException out of CompositionRoot.Build instead of a pathUnavailable value.
  evidence: Unverified (medium if true). Settle by running UserPaths.ConfigFile in an Alpine container; if it throws, catch DllNotFoundException/EntryPointNotFoundException in NativeUserFolderSource.
