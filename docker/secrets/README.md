# Local password files

Create these two files here before starting Compose:

- `postgres-admin-password.txt`: a new local database administrator password.
- `app-db-password.txt`: a different new application database password.

Use one line per file, UTF-8 without BOM, and letters/numbers for this exercise.
These files are ignored by `docker/.gitignore` and excluded from the image build.
Do not put real production passwords here. The Stage 3 guide includes an optional
PowerShell snippet that generates both without overwriting existing files.

The files stay on your laptop; Compose mounts them into only the containers that need them.
Local Compose secrets are files, not an encrypted cloud secret store.
Changing a file later does not rotate an existing PostgreSQL role's password.
