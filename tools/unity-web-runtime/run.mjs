import assert from 'node:assert/strict';
import { createHash } from 'node:crypto';
import { createReadStream } from 'node:fs';
import { readFile, realpath, stat, writeFile } from 'node:fs/promises';
import { createServer } from 'node:http';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { chromium, firefox } from 'playwright';

const options = new Map();
for (let index = 2; index < process.argv.length; index++) {
  const key = process.argv[index];
  assert(['--project', '--browser', '--report', '--timeout', '--negative-control'].includes(key), `Unknown option: ${key}`);
  assert(!options.has(key), `Duplicate option: ${key}`);
  options.set(key, key === '--negative-control' ? true : process.argv[++index]);
}
assert(options.get('--project'), 'Usage: node run.mjs --project PATH [--browser chromium|firefox] [--negative-control]');
const project = await realpath(options.get('--project'));
const repository = await realpath(fileURLToPath(new URL('../..', import.meta.url)));
const examples = path.join(repository, 'examples');
const outsideExamples = value => assert(value !== examples && !value.startsWith(examples + path.sep), 'Validation must stay outside examples/.');
outsideExamples(project);
const browserName = options.get('--browser') ?? 'chromium';
assert(['chromium', 'firefox'].includes(browserName), 'Browser must be chromium or firefox.');
const negative = options.has('--negative-control');
const timeout = Number(options.get('--timeout') ?? 300000);
assert(Number.isSafeInteger(timeout) && timeout > 0, 'Timeout must be a positive number of milliseconds.');
const reportPath = path.resolve(options.get('--report') ?? path.join(project, `web-browser-${browserName}${negative ? '-negative' : ''}.json`));
outsideExamples(reportPath);
outsideExamples(await realpath(path.dirname(reportPath)));
try { outsideExamples(await realpath(reportPath)); } catch (error) { if (error.code !== 'ENOENT') throw error; }
const root = await realpath(path.join(project, 'Builds/WebRuntime'));
outsideExamples(root);
const build = JSON.parse(await readFile(path.join(project, 'unity-web-build-report.json'), 'utf8'));
const inputs = JSON.parse(await readFile(path.join(project, 'web-runtime-inputs.json'), 'utf8'));
const report = { status: 'fail', browser: browserName, negativeControl: negative, build, console: [], requests: [], pageErrors: [] };
const results = {};
let server;
let browser;
let finish;
const completed = new Promise(resolve => { finish = resolve; });
let timer;
try {
  assert.equal(build.status, 'pass');
  assert.equal(build.target, 'WebGL');
  assert.equal(build.backend, 'IL2CPP');
  assert.equal(build.managedStrippingLevel, 'High');
  assert.equal(build.developmentBuild, false);
  assert.equal(await realpath(build.outputPath), root);
  const mime = { '.html': 'text/html', '.js': 'application/javascript', '.wasm': 'application/wasm', '.data': 'application/octet-stream', '.json': 'application/json', '.css': 'text/css', '.png': 'image/png', '.ico': 'image/x-icon' };
  server = createServer(async (request, response) => {
    try {
      const pathname = decodeURIComponent(new URL(request.url, 'http://localhost').pathname);
      const file = await realpath(path.join(root, pathname === '/' ? 'index.html' : pathname));
      if (!file.startsWith(root + path.sep) || !(await stat(file)).isFile()) {
        response.writeHead(403).end();
        return;
      }
      response.writeHead(200, { 'Content-Type': mime[path.extname(file)] ?? 'application/octet-stream', 'Cache-Control': 'no-store', 'Cross-Origin-Opener-Policy': 'same-origin', 'Cross-Origin-Embedder-Policy': 'require-corp' });
      createReadStream(file).on('error', () => response.destroy()).pipe(response);
    } catch { response.writeHead(404).end(); }
  });
  await new Promise(resolve => server.listen(0, '127.0.0.1', resolve));
  browser = await (browserName === 'chromium' ? chromium : firefox).launch({
    headless: true,
    ...(browserName === 'chromium' ? { channel: 'chrome', args: ['--enable-unsafe-swiftshader'] } : {}),
  });
  report.browserVersion = browser.version();
  const page = await browser.newPage({ viewport: { width: 960, height: 640 } });
  // Unity truncates large Debug.Log messages. Read full reports through the
  // test player's JavaScript plugin, keeping console output as diagnostics.
  page.on('console', message => report.console.push({ type: message.type(), text: message.text() }));
  page.on('pageerror', error => { report.pageErrors.push(error.stack ?? error.message); finish(); });
  page.on('crash', () => { report.pageErrors.push('Browser page crashed.'); finish(); });
  page.on('response', response => { report.requests.push({ url: response.url(), status: response.status(), contentType: response.headers()['content-type'] }); });
  page.on('requestfailed', request => { report.pageErrors.push(`${request.url()}: ${request.failure()?.errorText}`); finish(); });
  const url = `http://127.0.0.1:${server.address().port}/${negative ? '?spireFailure=1' : ''}`;
  report.url = url;
  timer = setTimeout(() => { report.pageErrors.push(`No complete results after ${timeout}ms.`); finish(); }, timeout);
  await page.goto(url, { waitUntil: 'domcontentloaded', timeout });
  await Promise.race([
    page.waitForFunction(() => globalThis.__spireWebResults?.core && globalThis.__spireWebResults?.behavioral,
      undefined, { timeout }).then(async () => { Object.assign(results, await page.evaluate(() => globalThis.__spireWebResults)); })
      .catch(error => report.pageErrors.push(error.message)),
    completed,
  ]);
  report.results = results;
  report.browserRuntime = await page.evaluate(() => {
    const canvas = document.querySelector('canvas');
    const gl = canvas?.getContext('webgl2');
    const extension = gl?.getExtension('WEBGL_debug_renderer_info');
    return { userAgent: navigator.userAgent, webAssembly: typeof WebAssembly === 'object', webGL2: !!gl, renderer: gl && gl.getParameter(extension ? extension.UNMASKED_RENDERER_WEBGL : gl.RENDERER) };
  });
  assert.deepEqual(report.pageErrors, [], 'Browser runtime errors.');
  assert(report.browserRuntime.webAssembly && report.browserRuntime.webGL2, 'WebAssembly and WebGL2 must be active.');
  const wasm = report.requests.filter(request => new URL(request.url).pathname.endsWith('.wasm'));
  assert(wasm.length > 0 && wasm.every(request => request.status === 200 && request.contentType?.startsWith('application/wasm')), 'Wasm must load successfully with the correct MIME type.');
  for (const result of [results.core, results.behavioral]) {
    assert(result, 'Both native core and behavioral results are required.');
    assert.equal(result.unityVersion, build.unityVersion);
    assert.equal(result.backend, 'IL2CPP');
    assert.equal(result.runtimePlatform, 'WebGLPlayer');
    assert.equal(result.processorBits, 32);
    assert.equal(result.debugBuild, false);
  }
  assert.equal(results.core.status, 'pass');
  assert.deepEqual(results.core.failures, []);
  assert.equal(results.core.checks, build.unityVersion.startsWith('2022.') ? 59 : 60);
  const behavior = results.behavioral;
  assert.equal(behavior.expectedFacts, inputs.expectedFacts);
  assert.equal(behavior.executedFacts, inputs.expectedFacts);
  assert.deepEqual(behavior.cases.map(item => item.name).sort(), inputs.expectedFactNames);
  assert(behavior.cases.every(item => item.status === 'pass'), 'Every original Fact must pass.');
  assert.equal(behavior.forcedFailure, negative);
  if (negative) {
    assert.equal(behavior.status, 'fail');
    assert(behavior.failures.some(item => item.includes('Xunit.Sdk.TrueException') && item.includes('negative control')),
      'The original bridge must propagate the intentional xUnit assertion failure.');
    report.negativeControlObserved = true;
    throw new Error('Intentional negative control reached the browser and produced a failing result.');
  }
  assert.equal(behavior.status, 'pass');
  assert.deepEqual(behavior.failures, []);
  report.status = 'pass';
} catch (error) {
  report.error = error.stack ?? String(error);
} finally {
  clearTimeout(timer);
  if (browser) await browser.close();
  if (server) await new Promise(resolve => server.close(resolve));
  report.inputManifestSha256 = createHash('sha256').update(await readFile(path.join(project, 'web-runtime-inputs.json'))).digest('hex');
  await writeFile(reportPath, JSON.stringify(report, null, 2) + '\n');
}
console.log(JSON.stringify({ status: report.status, browser: browserName, version: report.browserVersion, unity: build.unityVersion, coreChecks: results.core?.checks, behavioralFacts: results.behavioral?.executedFacts, negativeControlObserved: report.negativeControlObserved ?? false, report: reportPath, error: report.error }));
process.exitCode = report.status === 'pass' ? 0 : 1;
