#!/bin/bash
# Gracefully stop the SAP NPL system (do this before `docker compose down`).
#   docker compose -f sap/docker-compose.yml exec abap /sap/stop.sh
set -e
su - npladm -c "stopsap ALL"
echo "SAP NPL stopped. Safe to 'docker compose -f sap/docker-compose.yml stop'."
