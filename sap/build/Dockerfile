# SAP NetWeaver AS ABAP 7.52 Developer Edition (SID NPL, SAP ASE) → Docker image.
#
# Build context must contain a `sapdownloads/` folder with the *extracted* contents
# of the SAP archives (so that `sapdownloads/install.sh` exists). See sap/build/build.sh
# and sap/README.md "Route B".
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

# Unattended-install expect wrapper, then the extracted SAP payload.
COPY install.exp /tmp/sapdownloads/
COPY sapdownloads /tmp/sapdownloads/

WORKDIR /tmp/sapdownloads
RUN chmod +x install.sh install.exp

# HTTP / HTTPS(ICM) / gateway (ADT) / SAP GUI dispatcher
EXPOSE 8000 44300 3300 3200

# The install writes to /proc (vm.max_map_count) and needs an interactive-ish TTY,
# so it cannot run at build time. Run it once after first `up`:
#   docker compose -f sap/docker-compose.yml exec abap ./install.exp
CMD ["tail", "-f", "/dev/null"]
