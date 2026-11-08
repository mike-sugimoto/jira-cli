- source_spec: `_bmad-output/implementation-artifacts/spec-1-1-solution-skeleton-with-architecture-guards.md`
  summary: Pin that CompositionRoot's ValidateOnBuild/ValidateScopes actually reject a bad registration once the first real service is registered (Story 1.2).
  evidence: Every Add…() registers nothing in 1.1, so removing either option or an Add…() call from CompositionRoot.Build leaves every test green (verification-gap review, pass 1).
