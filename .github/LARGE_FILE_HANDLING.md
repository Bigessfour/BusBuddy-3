# Large File Handling in BusBuddy-WPF

This document provides guidance on how to work with large files in the BusBuddy-WPF repository.

## GitHub File Size Limits

GitHub enforces the following file size restrictions:

- Files under 50MB: Fully supported
- Files 50MB-100MB: Warning triggered but allowed
- Files over 100MB: Rejected by GitHub

## Using Git LFS (Large File Storage)

For files larger than 50MB, we use Git LFS to handle them properly.

### Setting Up Git LFS

1. Install Git LFS:

   ```
   git lfs install
   ```

2. Clone this repository (if you haven't already):

   ```
   git clone https://github.com/Bigessfour/BusBuddy-WPF.git
   ```

3. Git LFS is already configured for this repository via the `.gitattributes` file.

### Adding New Large Files

When adding new large files to the repository:

1. Ensure the file type is tracked by Git LFS by checking `.gitattributes`
2. If it's a new file type, add it to tracking:
   ```
   git lfs track "*.your-extension"
   git add .gitattributes
   git commit -m "Track *.your-extension files with Git LFS"
   ```
3. Add and commit your large file normally:
   ```
   git add path/to/large/file
   git commit -m "Add large file"
   ```

## What Files Should Use Git LFS?

In the BusBuddy-WPF project, these file types are configured to use Git LFS:

- Design assets: `.psd`, `.ai`
- Documents: `.pdf` (large ones)
- Media: `.mp4`, `.mov`
- Archives: `.zip`, `.7z`
- Databases: `.mdf`, `.ldf`, `.ndf`, `.sqlite`

## Files That Should Not Be Committed

Some files should never be committed to the repository:

- Sensitive data (API keys, connection strings)
- Temporary build outputs
- Log files (except specified important logs)
- User-specific configuration files

See the `.gitignore` file for a complete list of excluded files.

## Questions?

If you have questions about handling large files in this repository, please open an issue on GitHub or contact the repository maintainers.
