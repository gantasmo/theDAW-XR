# Publishing the theDAW XR packages

The four features live as embedded UPM packages under `Packages/com.gantasmo.*`. They ship two
ways: a git URL that works today, and OpenUPM, a scoped registry that also resolves
package-to-package dependencies.

## Packages

| Package | Folder | Depends on |
|---|---|---|
| `com.gantasmo.questmidi` | `Packages/com.gantasmo.questmidi` | core |
| `com.gantasmo.midi-reactor` | `Packages/com.gantasmo.midi-reactor` | `com.gantasmo.questmidi` |
| `com.gantasmo.passthrough` | `Packages/com.gantasmo.passthrough` | standalone |
| `com.gantasmo.colocation` | `Packages/com.gantasmo.colocation` | standalone |

## Route 1: Git URL (no registry)

In Unity: **Window > Package Manager > + > Add package from git URL**.

- `https://github.com/gantasmo/theDAW-XR.git?path=/Packages/com.gantasmo.questmidi`
- `https://github.com/gantasmo/theDAW-XR.git?path=/Packages/com.gantasmo.midi-reactor`
- `https://github.com/gantasmo/theDAW-XR.git?path=/Packages/com.gantasmo.passthrough`
- `https://github.com/gantasmo/theDAW-XR.git?path=/Packages/com.gantasmo.colocation`

Append `#<tag>` to pin a release, for example
`...com.gantasmo.questmidi#com.gantasmo.questmidi/0.1.0`. UPM does not resolve
package-to-package dependencies across git URLs, so `com.gantasmo.questmidi` installs before
`com.gantasmo.midi-reactor`. This route needs the package changes pushed to the repo first.

## Route 2: OpenUPM (recommended for distribution)

OpenUPM is a free scoped registry plus a build bot that watches git tags, so dependencies
resolve automatically once each package is listed.

### One-time prep (already in this repo)

- An MIT `LICENSE` at the repo root and a `LICENSE.md` inside each package.
- Each `package.json` carries `license`, `repository` (with the `directory` subpath),
  `displayName`, `description`, `unity`, and `version`.
- A `CHANGELOG.md` per package.

### Release a version

OpenUPM reads versions from git tags. In a monorepo, tag each package with its name as the
prefix:

```
git tag com.gantasmo.questmidi/0.1.0
git tag com.gantasmo.midi-reactor/0.1.0
git tag com.gantasmo.passthrough/0.1.0
git tag com.gantasmo.colocation/0.1.0
git push origin --tags
```

Bump the version in the package's `package.json` and `CHANGELOG.md` before each new tag.

### Submit to OpenUPM

1. Open https://openupm.com and choose **Packages > Add Package**.
2. Enter the repository `gantasmo/theDAW-XR`. OpenUPM detects the monorepo and lists the four
   package folders.
3. For each package, confirm the package path (for example `Packages/com.gantasmo.questmidi`)
   and set the git tag prefix to the package name (for example `com.gantasmo.questmidi/`).
   OpenUPM opens a pull request against the `openupm/openupm` index.
4. After the pull request merges, OpenUPM builds every matching tag and serves it from the
   registry.

### Consumer install (after listing)

With the OpenUPM CLI:

```
openupm add com.gantasmo.questmidi
openupm add com.gantasmo.midi-reactor
```

Or add the scoped registry by hand in **Project Settings > Package Manager**:

- URL: `https://package.openupm.com`
- Scope: `com.gantasmo`

then install from **Window > Package Manager > My Registries**.

## Versioning

Semantic Versioning. A `0.x` version signals a pre-1.0 API that may still change. Tag `1.0.0`
once the API is stable.
