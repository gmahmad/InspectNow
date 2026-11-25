import assert from 'node:assert/strict';
import test from 'node:test';
import { canSubmit, createApiWorkspace, createDemoWorkspace, inspectProgress } from './workspace.js';

test('demo supports publishing, versioned answers, submission, and reload', async () => {
  const memory = new Map();
  const storage = { getItem: key => memory.get(key), setItem: (key, value) => memory.set(key, value) };
  const workspace = createDemoWorkspace(storage);
  const template = await workspace.createTemplate('Weekly safety check');
  await assert.rejects(workspace.publish(template.id), /question/);
  await workspace.addQuestion(template.id, { text: 'Exit clear?', isRequired: true });
  await workspace.addQuestion(template.id, { text: 'Signs visible?', isRequired: false });
  await workspace.publish(template.id);
  await assert.rejects(workspace.addQuestion(template.id, { text: 'New question' }), /finalized/);

  const { id } = await workspace.start(template.id, 'Warehouse');
  let inspection = await workspace.inspection(id);
  const { version, questions: [required, optional] } = inspection;
  assert.equal(canSubmit(inspection), false);
  await assert.rejects(workspace.answer(id, required.id, { answer: 'NotApplicable', expectedVersion: version }), /Pass or Fail/);
  await workspace.answer(id, required.id, { answer: 'Pass', comment: 'Checked', expectedVersion: version });
  await assert.rejects(workspace.answer(id, optional.id, { answer: 'NotApplicable', expectedVersion: version }), /changed/);
  inspection = await workspace.inspection(id);
  assert.equal(canSubmit(inspection), true);
  assert.deepEqual(inspectProgress(inspection), { total: 2, answered: 1, percent: 50 });
  await workspace.submit(id, inspection.version);

  const restored = await createDemoWorkspace(storage).inspection(id);
  assert.equal(restored.status, 'Submitted');
  assert.equal(restored.questions[0].comment, 'Checked');
  await assert.rejects(workspace.answer(id, required.id, { answer: 'Fail', expectedVersion: restored.version }), /finalized/);
});

test('API client sends workspace headers and preserves pagination and version tokens', async context => {
  const requests = [];
  context.mock.method(globalThis, 'fetch', async (url, options) => {
    requests.push({ url, ...options });
    if (url.includes('page=1')) return Response.json({ items: [{ id: 1 }], totalPages: 2 });
    if (url.includes('page=2')) return Response.json({ items: [{ id: 2 }], totalPages: 2 });
    return Response.json({ version: 'server-version' });
  });

  const workspace = createApiWorkspace('sample-token', 42);
  assert.deepEqual(await workspace.inspections(), [{ id: 1 }, { id: 2 }]);
  await workspace.answer(1, 2, { answer: 'Fail', comment: 'Needs attention', expectedVersion: 'current-version' });
  assert.equal(requests.length, 3);
  assert.equal(requests[0].headers.Authorization, 'Bearer sample-token');
  assert.equal(requests[0].headers['X-Organization-Id'], '42');
  assert.equal(requests[2].url, '/api/inspections/1/questions/2/answer');
  assert.equal(requests[2].method, 'PUT');
  assert.equal(JSON.parse(requests[2].body).expectedVersion, 'current-version');
});
