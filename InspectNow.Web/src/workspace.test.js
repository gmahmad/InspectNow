import assert from 'node:assert/strict';
import test from 'node:test';
import { canSubmit, createApiWorkspace, createDemoWorkspace, inspectProgress } from './workspace.js';
import { createDemoForms, formSections } from './forms-data.js';
import { recordId, routeFromHash, routeLink } from './routes.js';

test('workspace routes preserve nested response links and filters', () => {
  assert.deepEqual(routeFromHash('#assessments/responses/4?form=2&page=3'), {
    page: 'assessments', parts: ['responses', '4'], query: 'form=2&page=3',
  });
  assert.equal(recordId('4'), 4);
  assert.equal(recordId('0'), null);
  assert.equal(recordId('9007199254740993'), null);
  assert.equal(routeLink('assignments', 4), '#assignments/4');
});

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

test('cookie-authenticated assignment writes fetch and forward an antiforgery token', async context => {
  const requests = [];
  context.mock.method(globalThis, 'fetch', async (url, options) => {
    requests.push({ url, ...options });
    return url.endsWith('/csrf') ? Response.json({ token: 'test-csrf' }) : Response.json({ id: 4 });
  });

  const workspace = createApiWorkspace(undefined, 42);
  const assignment = { templateId: 2, siteId: 3, assigneeId: 'user-1', dueDate: '2027-01-10' };
  assert.deepEqual(await workspace.createAssignment(assignment), { id: 4 });
  assert.deepEqual(requests.map(request => request.url), ['/api/account/csrf', '/api/assignments']);
  assert.equal(requests[1].headers['X-CSRF-Token'], 'test-csrf');
  assert.equal(requests[1].headers['X-Organization-Id'], '42');
  assert.equal(requests[1].headers.Authorization, undefined);
  assert.deepEqual(JSON.parse(requests[1].body), assignment);
});

test('form sections and an explicit No answer survive draft save and submission', async () => {
  const memory = new Map();
  const storage = { getItem: key => memory.get(key), setItem: (key, value) => memory.set(key, value) };
  let demo = createDemoForms(storage);
  const question = { id: crypto.randomUUID(), type: 'boolean', label: 'Any defects?', required: true, options: [] };
  const form = await demo.createForm({ name: 'Receiving check', kind: 'General', fields: [
    { id: crypto.randomUUID(), type: 'section', label: 'Receiving', required: false, options: [] }, question,
  ] });
  const published = await demo.publishForm(form.id, form.version);
  assert.equal(formSections(published.fields)[0].label, 'Receiving');
  const saved = await demo.saveFormDraft(form.id, { answers: { [question.id]: false }, expectedVersion: null, submit: false });
  demo = createDemoForms(storage);
  assert.equal((await demo.formDraft(form.id)).answers[question.id], false);
  await demo.saveFormDraft(form.id, { answers: saved.answers, expectedVersion: saved.version, submit: true });
  assert.equal((await demo.formResponses(1, form.id)).total, 1);
  assert.equal((await demo.formDraft(form.id)).version, null);
});
