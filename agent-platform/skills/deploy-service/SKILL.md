# Deploy Service

Use this procedure when deploying a reviewed application release to production.

1. Read the service's canonical deployment procedure and identify the exact release.
2. Verify GitLab pipeline success and the deployment target.
3. Have the authorized deployment tool resolve its Infisical references internally.
   Keep secret values out of Brain memories, knowledge, manifests, and tool results.
4. Review the deployment change and obtain any approval required by the service's procedure.
5. Deploy the verified release through the configured Coolify integration.
6. Check application health and record the release and deployment outcome.
   Follow the service's rollback procedure if health checks fail.

Brain provides discovery metadata and these instructions. The caller's harness
must have independently configured external integrations and authorization;
discovering this skill does not execute a deployment.
