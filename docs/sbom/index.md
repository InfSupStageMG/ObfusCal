# Software Bill of Materials (SBOM)

This project publishes a CycloneDX SBOM for dependency transparency and security review.

## View the SBOM

- [Open SBOM JSON](./InfSupStageMG - SBOM.json)

## Notes

- Format: CycloneDX 1.6
- File location: `docs/sbom/InfSupStageMG - SBOM.json`

If your browser downloads the file instead of rendering it, open it in a JSON viewer/editor for easier inspection.

## Updating the SBOM

The SBOM is generated using the [CycloneDX .NET Tool](https://github.com/CycloneDX/cyclonedx-dotnet). If you add or
update NuGet packages in the solution, you must regenerate the SBOM before submitting a Pull Request.

Run the following commands from the repository root:

```bash
# Install the tool (if not already installed)
dotnet tool install --global CycloneDX

# Generate the updated SBOM
dotnet cyclonedx ObfusCal.slnx -o docs/sbom -j
```
