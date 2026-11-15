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
- source_spec: `_bmad-output/implementation-artifacts/spec-1-3-get-only-transport-with-contained-credentials-and-a-live-check.md`
  summary: Add `tools/JiraCli.LiveCheck` (never packaged, no arguments, F5-runnable) that loads the real config through Story 1.2's loader, sends `myself` and one `project/{key}` per allowed project through `IAtlassianTransport`, prints the endpoint, each status, and each body (never the token or `Authorization` value), and records any difference from AD-12 before Story 1.6.
  evidence: Split from Story 1.3 at the spec size gate (about 2,000 tokens against the 1,600 target); the transport is independently shippable and LiveCheck only consumes it. Epic AC for 1.3 still includes the live check, so it must land before 1.6 starts.
- source_spec: `_bmad-output/implementation-artifacts/spec-1-3-get-only-transport-with-contained-credentials-and-a-live-check.md`
  summary: Test the under-base-URL guard in `HttpAtlassianTransport.BuildUri` (a URI outside the base is refused and no request is sent).
  evidence: Escaping neutralises every input today, so the guard is unreachable through the public surface and deleting it keeps all tests green (verification-gap review). Add the test with a seam when a second URI-building path appears (Story 4.2 `Download`).
  status: resolved in Story 1.3 (human request after review, 2026-11-15) — URI building and the under-base check moved into internal `RequestUriBuilder` (behind `IRequestUriBuilder`); `RequestUriBuilderTests` calls `EnsureUnderBase` directly with outside-base URIs.
