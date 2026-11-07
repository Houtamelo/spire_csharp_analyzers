# Signed tarball acceptance

Run `Spire.Tool unity-tarball --package package.tgz --project tmp/customer
--editor /path/to/Editor/Unity`. Add `--prepare-only` for filesystem preparation.
See [release commands](../../docs/upm-release.md).

The actual Editor installs the archive, checks its signature and every payload
file, verifies compiler DLL import settings, imports Quick Start using Package
Manager, then restarts and verifies compiled APIs and native JSON persistence.
The negative accessor check expects the generator's Unity assertion, not an
exception. A separate host comparison preserves every artifact manifest value
and permits only the measured Editor-generated `_fingerprint` cache field.

Unity 6000 must report Valid. The explicit older-Editor option records Unsigned
or Unchecked as a limitation, never as cryptographic verification. Test a
one-byte tampered artifact on Unity 6000 and require failure. Each run uses a
disposable project; the fixture and live examples remain untouched.
