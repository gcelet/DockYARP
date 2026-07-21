## ADDED Requirements

### Requirement: Image publishing
The build pipeline SHALL publish the Docker image to a configurable container registry, defaulting to
Docker Hub, tagged with a configurable tag (default `latest`). The image SHALL be built through the Nuke
pipeline (the Docker build stage runs the build), and publishing SHALL assume the environment is already
authenticated to the registry.

#### Scenario: Publish to the default registry
- **WHEN** the publish target runs without a registry override
- **THEN** the image is built via the pipeline and pushed as `{repository}:{tag}` to Docker Hub

#### Scenario: Publish to a custom registry
- **WHEN** a registry host is provided
- **THEN** the image is pushed as `{registry}/{repository}:{tag}`
