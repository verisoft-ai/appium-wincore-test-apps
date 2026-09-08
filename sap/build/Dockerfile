# Base image for SAP NetWeaver AS ABAP 7.52 Developer Edition (SID NPL, SAP ASE).
#
# The multi-GB SAP install payload is NOT baked in — docker-compose.yml bind-mounts
# it at /tmp/sapdownloads and ./install.exp consumes it once on first run, installing
# into the /sybase, /usr/sap and /sapmnt volumes. Keeps this image ~500 MB and lets
# you delete sap/build/sapdownloads/ after the install succeeds.
#
# Based on the community Dockerfiles by Gregor Wolf, Tobias Hofmann and Nabi Zamani
# (github.com/nzamani/sap-nw-abap-trial-docker) — trimmed and pinned.

FROM opensuse/leap:15.6

ENV LANG=en_US.UTF-8

# Packages the SAP installer + ASE + ABAP runtime need.
RUN zypper --non-interactive install --replacefiles \
      which hostname expect net-tools iputils wget vim iproute2 unrar less \
      tar gzip uuidd tcsh libaio1 \
 && zypper clean --all

# uuidd must exist and have run once before the ABAP dispatcher will start.
RUN mkdir -p /run/uuidd && chown uuidd /run/uuidd && /usr/sbin/uuidd || true

# HTTP / HTTPS(ICM) / gateway (ADT) / SAP GUI dispatcher
EXPOSE 8000 44300 3300 3200

# Container just needs to stay up; SAP is installed/started via `compose exec`.
CMD ["tail", "-f", "/dev/null"]
