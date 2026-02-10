# US-008: File Claiming Mechanism with Cross-Volume Support

**As a** Developer,
**I want to** implement a Claimer that moves files from inbox to processing with cross-volume fallback,
**So that** files are safely claimed by the service and can be processed even when inbox and processing are on different volumes.

## Acceptance Criteria
- [ ] **Scenario 1**: Claim File with Same-Volume Move
    - **Given** inbox and processing are on the same volume
    - **When** the claimer moves a file from `<Root>\inbox\file.pdf` to `<Root>\processing\<jobId>\file.pdf`
    - **Then** the file is moved atomically using File.Move and the new path is returned

- [ ] **Scenario 2**: Create Processing Subdirectory for Job
    - **Given** a new job is being claimed
    - **When** the claimer moves the file to processing
    - **Then** the directory `<Root>\processing\<jobId>\` is created if it doesn't exist

- [ ] **Scenario 3**: Handle Cross-Volume Move with Copy-Verify-Delete
    - **Given** inbox and processing are on different volumes or shares
    - **When** File.Move fails or a cross-volume scenario is detected
    - **Then** the claimer performs: copy to temp name → verify size (and optionally checksum) → delete source → rename to final name

- [ ] **Scenario 4**: Verify Copy Integrity for Cross-Volume Operations
    - **Given** a cross-volume copy is performed
    - **When** the copy completes
    - **Then** the destination file size is verified to match the source; optionally, a checksum is computed and compared

- [ ] **Scenario 5**: Handle File Already Claimed
    - **Given** a file in inbox was already claimed by a previous scan
    - **When** the claimer attempts to move it
    - **Then** the FileNotFoundException is caught, logged as "already claimed", and no duplicate job is created

- [ ] **Scenario 6**: Handle Transient Move Failures
    - **Given** the move operation fails due to transient errors (network hiccup)
    - **When** the move is attempted
    - **Then** the error is logged, the file is left in inbox, and the claim will be retried on the next scan

- [ ] **Scenario 7**: Transactional State Recording
    - **Given** the file move succeeds
    - **When** the claim operation completes
    - **Then** the job status is updated to Claimed and processing_path is recorded in SQLite within a transaction

## Priority
- [x] High (Must Have)
- [ ] Medium (Should Have)
- [ ] Low (Nice to Have)

## Technical Notes / Assumptions
- Preferred: atomic File.Move for same-volume operations
- Fallback: copy-verify-delete for cross-volume scenarios
- Create processing subdirectory: `<Root>\processing\<jobId>\`
- Cross-volume detection: catch IOException or check if source and destination are on different volumes
- Verification for cross-volume:
  - Size check (required)
  - Checksum (optional for v1, recommended for future)
- All claim state transitions must be transactional in SQLite
- Handle exceptions: FileNotFoundException (already claimed), IOException (transient retry), UnauthorizedAccessException (permission error)
- Return ClaimResult with: Success, AlreadyClaimed, TransientFailure, PermanentFailure
