# Proxmox nacht-02

This is sample development knowledge, not a statement about a live host.

## Cluster recovery

If a Proxmox cluster join fails because corosync.conf is missing, inspect cluster configuration and service logs before retrying. Record observed facts separately from proposed recovery actions.

## Change coordination

Read canonical knowledge through the Brain and retain the returned revision token. A future knowledge-write proposal must reference the version that was read.
