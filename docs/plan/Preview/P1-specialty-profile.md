# Preview — Agent Definition Extension: Specialty Profile [Target]

Objective: extend `AgentDefinition` with a first-class specialty profile (responsibilities, exclusions, expected output, tool capabilities, success criteria) so that uniqueness, matching, and guard decisions can be made deterministically.

Scope:
- specialty profile fields as first-class configuration (not free-text instructions);
- AgentDefinition versioning of the profile;
- lineage/parentage records preserved;
- no runtime promotion or generation change;
- current narrower `AgentDefinition` contract remains valid until this slice lands.

Verify:
- profile round-trips through persistence;
- uniqueness fingerprint derived from normalized profile;
- exact duplicate resolves to one specialist;
- semantic overlap detection compares responsibilities/exclusions/capabilities;
- configuration inheritance never grants authorization or credentials.
