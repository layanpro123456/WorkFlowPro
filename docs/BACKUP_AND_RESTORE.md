# Jiva – Database Backup & Recovery Runbook

This document defines the operational procedures for backing up and restoring the PostgreSQL 17 database underpinning Jiva, whether running via Docker Compose or in a managed containerized/cloud environment.

---

## 1. Recovery Objectives

| Metric | Target | Description |
|---|---|---|
| **Recovery Point Objective (RPO)** | $\le$ 1 hour | Maximum acceptable data loss window during a disaster scenario. |
| **Recovery Time Objective (RTO)** | $\le$ 30 minutes | Target time from disaster declaration to restored read/write service availability. |
| **Backup Integrity Verification** | Weekly | Automated test restore into an ephemeral isolated container with schema and row-count verification. |

---

## 2. Backup Strategy

WorkFlowPro uses a multi-tiered backup architecture:

1. **Logical Backups (`pg_dump -Fc`)**:
   - PostgreSQL custom-format archive (`-Fc`), which is compressed by default and supports selective or parallel restoration via `pg_restore`.
   - Executed hourly/daily and transferred immediately to off-host, encrypted object storage (e.g., AWS S3, Cloudflare R2, or Azure Blob Storage).
2. **Write-Ahead Logging (WAL) / Continuous Archiving (Production Enterprise)**:
   - For mission-critical environments requiring sub-minute RPO, enable continuous WAL archiving with `pg_basebackup` for Point-In-Time Recovery (PITR).

---

## 3. Manual Backup Operations

### 3.1. Creating a Full Logical Backup

Run the following command against the running `workflowpro-db` container:

```bash
# 1. Create a timestamped backup directory if it does not exist
mkdir -p ./backups

# 2. Execute pg_dump using custom format (-Fc)
docker exec -t workflowpro-db pg_dump \
  -U workflowpro_app \
  -d workflowpro \
  -Fc \
  -v \
  > ./backups/workflowpro_backup_$(date +%Y%m%d_%H%M%S).dump
```

*Flags explained:*
- `-U workflowpro_app`: Specifies the database owner user.
- `-d workflowpro`: Target database name.
- `-Fc`: Custom archive format with built-in compression and table-level restore flexibility.
- `-v`: Verbose progress output logged to stderr.

### 3.2. Plaintext SQL Dump (Schema & Data Diagnostics)

If a human-readable SQL script is required for auditing or schema inspection:

```bash
docker exec -t workflowpro-db pg_dump \
  -U workflowpro_app \
  -d workflowpro \
  --clean \
  --if-exists \
  --create \
  > ./backups/workflowpro_plain_$(date +%Y%m%d_%H%M%S).sql
```

---

## 4. Restoration Runbook

> [!CAUTION]
> Restoring a database drops existing tables and overwrites active data. Always stop API traffic before initiating a restore.

### Step 1: Drain Traffic and Stop API
Prevent active transactions or partial writes while restoring:

```bash
docker compose stop api
```

### Step 2: Verify Backup File Integrity
Verify the archive header using `pg_restore -l`:

```bash
# List archive contents to confirm file is uncorrupted
pg_restore -l ./backups/workflowpro_backup_YYYYMMDD_HHMMSS.dump | head -n 30
```

### Step 3: Execute Restoration

Transfer the dump file into the container or stream directly via standard input:

```bash
# Option A: Direct stream restore via docker exec
cat ./backups/workflowpro_backup_YYYYMMDD_HHMMSS.dump | docker exec -i workflowpro-db pg_restore \
  -U workflowpro_app \
  -d workflowpro \
  --clean \
  --if-exists \
  --no-owner \
  --no-privileges \
  -v
```

*Flags explained:*
- `--clean`: Drops database objects before recreating them.
- `--if-exists`: Adds `IF EXISTS` to drop statements, avoiding errors if objects are missing.
- `--no-owner`: Omits commands to set ownership, maintaining current database owner.
- `--no-privileges`: Prevents permission collision if roles differ between environments.

### Step 4: Restart Services & Verify Health
```bash
# Start backend API and verify health checks
docker compose up -d api
docker compose logs -f api
```

Verify that the health check endpoint returns healthy:
```bash
curl -f http://localhost:8080/health/ready
```

---

## 5. Automated Backup Script (`backup.sh`)

In production environments, schedule this shell script via cron or a Kubernetes CronJob:

```bash
#!/usr/bin/env bash
set -euo pipefail

BACKUP_DIR="/var/backups/workflowpro"
TIMESTAMP=$(date +"%Y%m%d_%H%M%S")
BACKUP_FILE="${BACKUP_DIR}/workflowpro_${TIMESTAMP}.dump"
LOG_FILE="/var/log/workflowpro_backup.log"
RETENTION_DAYS=30

mkdir -p "${BACKUP_DIR}"

echo "[$(date -u)] Starting WorkFlowPro automated backup..." >> "${LOG_FILE}"

# Execute pg_dump
if docker exec -t workflowpro-db pg_dump -U workflowpro_app -d workflowpro -Fc > "${BACKUP_FILE}"; then
    echo "[$(date -u)] Backup successful: ${BACKUP_FILE} ($(du -h "${BACKUP_FILE}" | cut -f1))" >> "${LOG_FILE}"
else
    echo "[$(date -u)] ERROR: Backup failed!" >> "${LOG_FILE}"
    exit 1
fi

# Optional: Encrypt backup with GPG before offloading
# gpg --encrypt --recipient ops@company.com "${BACKUP_FILE}"

# Optional: Upload to S3 / Cloud Storage
# aws s3 cp "${BACKUP_FILE}" "s3://company-db-backups/workflowpro/daily/"

# Prune local backups older than retention window
find "${BACKUP_DIR}" -name "workflowpro_*.dump" -type f -mtime +${RETENTION_DAYS} -delete
echo "[$(date -u)] Pruned backups older than ${RETENTION_DAYS} days." >> "${LOG_FILE}"
```

### Scheduling with Crontab
Add to `/etc/cron.d/workflowpro-backup`:
```text
# Run backup daily at 02:00 UTC
0 2 * * * root /usr/local/bin/backup.sh >> /var/log/cron_backup.log 2>&1
```

---

## 6. Disaster Recovery & Test Restores

To ensure backups are not merely "write-only", maintain this quarterly drill:
1. Spin up an isolated PostgreSQL container:
   ```bash
   docker run --name workflowpro-restore-drill -e POSTGRES_PASSWORD=drillpass -e POSTGRES_USER=workflowpro_app -e POSTGRES_DB=workflowpro -d postgres:17-alpine
   ```
2. Restore the latest production backup dump into `workflowpro-restore-drill`.
3. Execute validation queries:
   ```bash
   docker exec -i workflowpro-restore-drill psql -U workflowpro_app -d workflowpro -c "SELECT count(*) FROM users; SELECT count(*) FROM tasks; SELECT count(*) FROM support_tickets;"
   ```
4. Tear down the drill container:
   ```bash
   docker rm -f workflowpro-restore-drill
   ```
