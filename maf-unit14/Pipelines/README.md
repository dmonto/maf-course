# Pipeline template

Copy `unit14-container.yml` to `.github/workflows/unit14-container.yml` in the application repository when intentionally enabling the workflow. It expects the complete `maf-unit14` folder at the repository root.

The Ubuntu job restores locked dependencies, compiles all projects, runs the local suite, builds a Podman candidate, probes it in fixture mode and uploads evidence. Its `pwsh` shell belongs to the hosted Linux runner. The separate Windows job explicitly uses `shell: powershell` to run the native suite and command simulations in Windows PowerShell 5.1, with no local PowerShell upgrade. It does not run a real Windows container engine. The template does not publish the image or deploy cloud resources. Add the approved evaluation, security, image-scan, registry publication and Azure-mode verification stages to the release process before production promotion.

The isolated PowerShell command-contract checks use local functions in a child process. Their synthetic CLI responses do not establish any Azure, Podman or Kubernetes integration success.
