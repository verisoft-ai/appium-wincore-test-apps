# Base image for SAP NetWeaver AS ABAP 7.52 SP04 Developer Edition (SID NPL, SAP ASE).
#
# oraclelinux:7 → glibc 2.17. The ASE 16.0 SP03 dataserver was built for SLES 11
# (glibc 2.11) and runs trampolines on its engine-thread stacks. glibc 2.31+ (Leap
# 15.4+, Debian 12, …) makes pthread stacks non-executable regardless of PT_GNU_STACK,
# so the dataserver SIGSEGVs in dsinit / Snap::Validate with a W^X instruction-fetch
# fault (siginfo err 0x15). glibc 2.17 still gives thread stacks the main binary's
# PT_GNU_STACK executability, so it runs. Oracle Linux 7 repos are still maintained.
#
# The multi-GB SAP install payload is NOT baked in — docker-compose.yml bind-mounts it
# at /tmp/sapdownloads and ./install.exp consumes it once on first run, installing into
# the /sybase, /usr/sap and /sapmnt volumes.
#
# Based on the community Dockerfiles by Gregor Wolf, Tobias Hofmann and Nabi Zamani.

FROM oraclelinux:7

ENV LANG=en_US.UTF-8

# SWPM/sapinst + ASE + the ABAP runtime deps.
RUN yum -y -q install \
      expect tcsh libaio glibc-common which hostname uuidd \
      iproute net-tools iputils wget vim less tar gzip util-linux \
 && yum clean all && rm -rf /var/cache/yum \
 && test -x /usr/sbin/uuidd   # install.sh expects /sbin/uuidd; /sbin -> /usr/sbin on OL7

RUN mkdir -p /run/uuidd && chown uuidd /run/uuidd && /usr/sbin/uuidd || true

EXPOSE 8000 44300 3300 3200
CMD ["tail", "-f", "/dev/null"]
