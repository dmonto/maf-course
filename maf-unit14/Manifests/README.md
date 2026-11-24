# Kubernetes manifests

`Original` retains the GitBook YAML listings unchanged. Assigned-value markers in those listings need real environment values before applying.

The runnable section v03 renders equivalent complete Namespace/Deployment/Service manifests as JSON beneath its new artifact directory. v04 renders the HPA. `Scripts/Deploy-Aks.ps1` combines them, removes the fixed replica count when HPA is selected, adds workload identity configuration for Azure mode and applies only with `-Apply`.

JSON is accepted directly by kubectl. The default plan remains offline. Registry image-read permissions, OIDC/federated credentials, model roles and Metrics Server are existing infrastructure prerequisites.
