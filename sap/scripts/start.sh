#!/bin/bash
# Start the SAP NPL system inside the running container.
#   docker compose -f sap/docker-compose.yml exec abap /sap/start.sh
set -e

# uuidd must be running before the ABAP dispatcher starts.
/usr/sbin/uuidd || true

su - npladm -c "startsap ALL"

echo
echo "SAP NPL starting. Watch readiness with:"
echo "  docker compose -f sap/docker-compose.yml exec abap su - npladm -c 'sapcontrol -nr 00 -function GetProcessList'"
echo "All processes GREEN = ready. Then connect SAP GUI to vhcalnplci, instance 00, client 001."
