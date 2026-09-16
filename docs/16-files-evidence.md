# 16 — File Storage & Evidence Management

> 🚫 **DEFERRED — NOT IN MVP SCOPE (ADR-029).**
> No generic file-management subsystem is built. File storage enters scope **only** when an already-approved workflow explicitly requires an attachment. **No approved workflow currently does:** receiving, supply requests, fulfilment, dispatch, receipt confirmation, discrepancies, consumption, and stock counts all complete without one.
>
> **Deferral is not deletion.** This specification remains valid and intact so that the later phase starts from a written baseline rather than a fresh guess. Its schema support (`stored_files`, `file_attachments`, `docs/29 §4.2`) is specified but **not migrated** until the phase is scheduled.
>
> **Do not** build a file subsystem merely because file infrastructure was mentioned in an earlier document.

> **Document ID:** SPEC-16  
> **Topic:** Storage Abstraction, MIME Sniffing, Path Traversal Prevention, and Tenant Isolation

---

## 1. Storage Abstraction Architecture

Files (e.g., supplier delivery notes, discrepancy photos, physical count variance evidence) are managed through the `IFileStorageService` abstraction:

```
[ Upload Multipart Request ] ──► [ MIME Sniffing & Validation ] ──► [ Compute SHA256 ]
                                                                             │
                                                                             ▼
┌─────────────────────────────────────────────────────────┐     ┌────────────────────────┐
│ StoredFile Entity in PostgreSQL                         │     │ Storage Target         │
│ (id, company_id, storage_key, original_name, mime, sha) │     │ (Local Disk / S3 Blob) │
└─────────────────────────────────────────────────────────┘     └────────────────────────┘
```

---

## 2. Security Guardrails & Validation

1. **Safe Storage Key Generation:**
   - Client-provided file names and filesystem paths are **never** used for physical storage on disk.
   - Storage key format: `tenants/{companyId}/{year}/{month}/{random_uuid}.{safe_extension}`.
2. **MIME Sniffing & Extension Whitelist:**
   - Content inspection validates binary magic numbers.
   - Allowed MIME types: `image/jpeg`, `image/png`, `image/webp`, `application/pdf`.
   - Executable types (`.exe`, `.sh`, `.php`, `.js`, `.bat`) are rejected immediately.
3. **Tenant & Scope Authorization:**
   - Downloading or viewing a file requires a valid session belonging to the file's `companyId`.
   - Direct unauthenticated URL access to stored files is prohibited.
