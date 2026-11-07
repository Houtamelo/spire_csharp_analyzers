# Unity Editor HTTPS failure on openSUSE

The publisher's native Unity 6000.5.6f1 session reports My Assets OAuth failure
with response code 0 at `https://api.unity.com/v1/oauth2/authorize`. The official
publishing SDK independently reports an SSL connection failure. Hub's account
is signed in, and acquiring the publishing tool already resolved its separate
HTTP 403 entitlement error.

## Confirmed baseline

An isolated Unity Editor project, without Spire or account credentials, probes
the same public endpoint using both `System.Net.Http.HttpClient` and
`UnityWebRequest`, with default certificate verification:

- Managed HTTP: no HTTP status; UnityTls throws `CryptographicException: Chain
  instance is empty` inside the SSL handshake.
- Native HTTP: no HTTP status; `Unable to complete SSL connection` and UnityTls
  error code 7.
- The host's `curl --cacert /etc/ssl/ca-bundle.pem` reaches the endpoint with
  successful certificate verification. HTTP 400 is expected for this probe,
  which omits OAuth request parameters.

File-access tracing of that owned Editor confirms native reads of
`/etc/ssl/certs/ca-certificates.crt` and the fallback
`/etc/ssl/certs/ca-bundle.crt`; both return `ENOENT`. Host-level checks confirm
these paths are actually missing, independently of sandbox visibility.
openSUSE's existing trusted bundle, `/etc/ssl/ca-bundle.pem`, is readable.

Evidence: `tmp/unity-tls-20261107/tls-before.json`, `ca-path-evidence.txt`,
`ca-paths.strace`, and `editor.log`. The isolated process uses neither Hub tokens
nor a certificate-validation override. The original live-Editor probe command
ran against an older loaded automation assembly before Editor refresh; its
unsupported-command result is not networking evidence.

## Verified targeted repair

Create one compatibility symlink to the existing OS trust bundle:

```sh
sudo ln -s /etc/ssl/ca-bundle.pem /etc/ssl/certs/ca-certificates.crt
```

No force/overwrite flag is used. The target already contains the system's
trusted certificates; this adds an alternate path to that same trust store.
Non-interactive sudo requires a password, so the publisher was asked to run the
command in their terminal. The publisher completed it, and the link resolves to
the existing system bundle.

Afterward, rerun the same strict Unity HTTPS probes, then retry the official
publishing SDK in the native Hub-launched Editor. If the existing process cached
the empty trust store, restart that Editor through Hub and repeat. Preserve the
before/after reports. No upload should be reported until its actual completion
and remote version state are verified.

The same isolated strict probe now returns HTTP **400** through both managed
and native Unity clients, with no managed SSL exception. This is the expected
response to the deliberately incomplete OAuth request. Both previously failed
before any HTTP response. See `tls-before.json` and `tls-after.json` under the
evidence root.

The already-running native publishing Editor also authenticated through the
official SDK afterward, without a restart. It found publisher **Houtamelo**,
the intended draft **6893155**, the exact package technical name, and a matching
manifest author. Evidence:
`tmp/upm-minimum-20261107/publishing/automation-inspect-after-ca-repair-1.json`.

## Why it appeared during publishing

The same TLS error is already present in
`tmp/upm-release-20261106/publishing/preparation.log`, whose recorded modification
time is November 7 at 01:54 UTC, before the publisher's My Assets report. Local
Editor/player validation and Hub/Node-based downloads did not establish that
the Editor's own HTTPS clients worked.

RPM records show both CA packages installed on November 3, 2026 at 05:53 local
time; the generated PEM directory and bundle have matching timestamps. The
installed CA package's post-transaction script forces a trust-store rebuild.
Its `80etc_ssl.run` hook regenerates the PEM directory that `/etc/ssl/certs`
points to. An isolated reproduction using that same `trust extract` operation
removes an added `ca-certificates.crt` compatibility symlink. Evidence:
`tmp/unity-tls-20261107/regeneration-control/result.json`.

Thus a certificate update could have removed a previous workaround, but there
is no evidence that this alias existed before the update. Do not present that
historical cause as confirmed. The missing paths and current TLS failure are
confirmed. A one-off compatibility link can also be removed by a future rebuild;
a lasting repair must account for that regeneration.

A candidate local update hook is prepared at
`tmp/unity-tls-20261107/zz-unity-ca-compat.run`. The installed updater merges hooks
from `/etc/ca-certificates/update.d` and `/usr/lib/ca-certificates/update.d` by
filename, so a `zz-` local hook runs after PEM/bundle generation. It only creates
the missing alias when the system bundle exists, leaves existing files and
links untouched, and honors the updater's `destdir` for filesystem-isolated
testing. Real `trust extract` regeneration controls verify creation,
idempotence, removal by regeneration, and restoration afterward. The hook is
installed on the host by the publisher at
`/etc/ca-certificates/update.d/zz-unity-ca-compat.run`, owned by root with mode
0755. Its contents match the tested candidate. Tests of the installed hook in
an isolated filesystem verify missing-alias creation, idempotence, restoration
after real trust-store regeneration, and preservation of existing files. The
live host alias remains valid. See `tmp/unity-tls-20261107/installed-hook-result.json`.
