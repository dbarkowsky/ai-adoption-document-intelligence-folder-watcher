# US-006: Inbox Enumeration and File Filtering

**As a** Developer,
**I want to** implement an InboxEnumerator that scans the inbox and applies eligibility filters,
**So that** only valid files matching configured criteria are processed by the service.

## Acceptance Criteria
- [ ] **Scenario 1**: Enumerate Files in Inbox Directory
    - **Given** the inbox folder contains multiple files
    - **When** the InboxEnumerator scans the inbox
    - **Then** all files directly under `<Root>\inbox` are discovered (non-recursive by default)

- [ ] **Scenario 2**: Filter by Allowed Extensions
    - **Given** the configuration specifies AllowedExtensions = [".pdf", ".docx", ".tif"]
    - **When** the enumerator scans files
    - **Then** only files with matching extensions are included; all others are ignored

- [ ] **Scenario 3**: Filter by Maximum File Size
    - **Given** the configuration specifies MaxFileSizeMB = 10
    - **When** the enumerator scans files
    - **Then** files larger than 10 MB are filtered out and logged as oversized

- [ ] **Scenario 4**: Ignore System and Hidden Files
    - **Given** the inbox may contain temporary or system files
    - **When** the enumerator scans files
    - **Then** files with FileAttributes.Hidden or FileAttributes.System are ignored, and files starting with "~" or other configured ignore patterns are skipped

- [ ] **Scenario 5**: Optional Recursive Scanning
    - **Given** the configuration enables recursive scanning
    - **When** the enumerator scans the inbox
    - **Then** files in subdirectories are also discovered and processed

- [ ] **Scenario 6**: Handle Inaccessible or Missing Inbox
    - **Given** the inbox folder is temporarily unavailable (network issue)
    - **When** the enumerator attempts to scan
    - **Then** the error is logged and the scan is retried on the next interval without crashing the service

## Priority
- [x] High (Must Have)
- [ ] Medium (Should Have)
- [ ] Low (Nice to Have)

## Technical Notes / Assumptions
- Enumerate files using Directory.EnumerateFiles() for efficiency
- Default to non-recursive scanning (SearchOption.TopDirectoryOnly)
- Apply filters in this order: extension → size → ignore patterns
- Log filtered-out files at Debug level for troubleshooting
- Configuration:
  - AllowedExtensions: string array (e.g., [".pdf", ".docx", ".tif"])
  - MaxFileSizeMB: integer
  - IgnorePatterns: string array (e.g., ["~*", ".*"])
  - RecursiveScan: boolean (default false)
- Handle exceptions during enumeration (access denied, network timeout) gracefully
- Return IEnumerable<FileInfo> for eligible files
