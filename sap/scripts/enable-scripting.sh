#!/bin/bash
# Enable SAP GUI Scripting server-side without needing the RZ11 GUI transaction.
# Sets the profile parameter persistently in the instance profile and (best effort)
# dynamically for the running system.
#
#   docker compose -f sap/docker-compose.yml exec abap /sap/enable-scripting.sh
#
# You still must enable it CLIENT-side in SAP GUI Options → Accessibility & Scripting
# → Scripting → "Enable scripting", and untick the two "Notify when a script…" boxes.
set -e

PROFILE=$(ls /usr/sap/NPL/SYS/profile/NPL_D*vhcalnplci 2>/dev/null | head -1)
if [ -z "$PROFILE" ]; then
  echo "Instance profile not found under /usr/sap/NPL/SYS/profile/ — is the system installed?"
  exit 1
fi

if grep -q '^sapgui/user_scripting' "$PROFILE"; then
  sed -i 's|^sapgui/user_scripting.*|sapgui/user_scripting = TRUE|' "$PROFILE"
else
  echo 'sapgui/user_scripting = TRUE' >> "$PROFILE"
fi
echo "Set in profile: $PROFILE"
grep 'user_scripting' "$PROFILE"

echo
echo "Restart SAP for it to take effect:"
echo "  /sap/stop.sh && /sap/start.sh"
echo
echo "Verify after restart, in the SAP GUI command box: /nRZ11 → sapgui/user_scripting"
