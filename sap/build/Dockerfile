# Base image for SAP NetWeaver AS ABAP 7.52 SP04 Developer Edition (SID NPL, SAP ASE).
#
# openSUSE Leap 15.3: SP04's SWPM/sapinst (2018) and ASE 16.0 do NOT work on Leap 15.5+
#   - sapinst can't parse a 15.6 os-release ("Linux distribution could not be detected")
#   - ASE's `dataserver` fails its version probe on newer glibc, so sapinst asserts
#     "does not support the database server version 15.7.0.000"
# 15.3 is the newest Leap the community images run this on. Its repos are archived but
# still resolvable.
#
# The multi-GB SAP install payload is NOT baked in — docker-compose.yml bind-mounts it
# at /tmp/sapdownloads and ./install.exp consumes it once on first run, installing into
# the /sybase, /usr/sap and /sapmnt volumes. Keeps this image small and lets you delete
# sap/build/sapdownloads/ after the install succeeds.
#
# Based on the community Dockerfiles by Gregor Wolf, Tobias Hofmann and Nabi Zamani
# (github.com/nzamani/sap-nw-abap-trial-docker).

FROM opensuse/leap:15.3

ENV LANG=en_US.UTF-8

# Packages SWPM/sapinst + ASE + the ABAP runtime need. 15.3 still ships libnsl.so.1 in
# the base, so no libnsl1 package here (unlike 15.6). The image's default repos still
# resolve as of 2026-09.
RUN zypper --non-interactive --gpg-auto-import-keys refresh \
 && zypper --non-interactive install --replacefiles --no-recommends \
      which hostname expect net-tools iputils wget vim iproute2 unrar less \
      tar gzip uuidd tcsh libaio1 libltdl7 insserv-compat glibc-locale \
 && zypper clean --all

RUN mkdir -p /run/uuidd && chown uuidd /run/uuidd && /usr/sbin/uuidd || true

EXPOSE 8000 44300 3300 3200
CMD ["tail", "-f", "/dev/null"]
