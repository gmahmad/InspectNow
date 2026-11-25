const demoKey = 'inspectnow-demo-v1';

export const statusLabel = status => status === 'Draft' ? 'In progress' : status;

const checklists = [
  ['Facility Safety Audit', ['Are all emergency exits clear?', 'Are fire extinguishers within their service dates?', 'Is safety signage clearly visible?']],
  ['Equipment Readiness', ['Has the equipment passed its visual check?', 'Are guards and safety devices in place?', 'Are maintenance records current?']],
  ['Office Cleaning Checklist', ['Are the floors clean and dry?', 'Are work surfaces and common areas clean?', 'Have waste bins been emptied?']],
  ['Warehouse Walkthrough', ['Are aisles and loading bays clear?', 'Is stock stored securely?', 'Are walkways properly marked?']],
];

function seedDemo() {
  const templates = checklists.map(([name, questions], index) => ({
    id: index + 1, name, status: index === 3 ? 'Draft' : 'Published',
    questions: questions.map((text, i) => ({ id: i + 1, text, isRequired: i < 2 })),
  }));
  const inspections = [
    ['North Distribution Center', 0, 1, false, 2],
    ['Riverside Plant', 1, 2, false, 1],
    ['Maple Street Office', 2, 3, true, 3],
    ['West Logistics Hub', 0, 5, true, 3],
    ['Cedar Works', 1, 6, false, 0],
    ['Harbor Point Office', 2, 8, true, 3],
  ].map(([siteName, templateIndex, daysAgo, complete, answers], index) => {
    const template = templates[templateIndex];
    const startedAtUtc = new Date(Date.now() - daysAgo * 86400000).toISOString();
    return {
      id: 106 - index, siteName, templateId: template.id, templateName: template.name,
      status: complete ? 'Submitted' : 'Draft', startedAtUtc,
      submittedAtUtc: complete ? startedAtUtc : null, version: 'initial',
      questions: template.questions.map((q, i) => ({ ...q, answer: i < answers ? 'Pass' : null, comment: null })),
    };
  });
  return { templates, inspections };
}

export function inspectProgress(inspection) {
  const total = inspection.questionCount ?? inspection.questions.length;
  const answered = inspection.answeredQuestionCount ?? inspection.questions.filter(q => q.answer).length;
  return { total, answered, percent: total ? Math.round(answered * 100 / total) : 0 };
}

export function canSubmit(inspection) {
  return inspection.status === 'Draft' && inspection.questions.every(q => !q.isRequired || ['Pass', 'Fail'].includes(q.answer));
}

export function createDemoWorkspace(storage = localStorage) {
  let state;
  try { state = JSON.parse(storage.getItem(demoKey)); } catch { /* Recover from invalid browser data. */ }
  state ??= seedDemo();

  const read = (items, id) => {
    const item = items.find(item => item.id === id);
    if (!item) throw new Error('This record could not be found.');
    return item;
  };
  const draft = (items, id) => {
    const item = read(items, id);
    if (item.status !== 'Draft') throw new Error('This record has been finalized.');
    return item;
  };
  const latest = (id, version) => {
    const item = draft(state.inspections, id);
    if (item.version !== version) throw new Error('This inspection changed. Reopen it and try again.');
    return item;
  };
  const save = () => storage.setItem(demoKey, JSON.stringify(state));
  const nextId = items => Math.max(0, ...items.map(item => item.id)) + 1;
  return {
    async templates() { return structuredClone(state.templates); },
    async inspections() { return structuredClone(state.inspections); },
    async template(id) { return structuredClone(read(state.templates, id)); },
    async inspection(id) { return structuredClone(read(state.inspections, id)); },
    async createTemplate(name) {
      const item = { id: nextId(state.templates), name, status: 'Draft', questions: [] };
      state.templates.unshift(item);
      save();
      return { id: item.id };
    },
    async addQuestion(id, question) {
      const item = draft(state.templates, id);
      item.questions.push({ id: nextId(item.questions), ...question });
      save();
    },
    async publish(id) {
      const item = draft(state.templates, id);
      if (!item.questions.length) throw new Error('Add a question before publishing.');
      item.status = 'Published';
      save();
    },
    async start(templateId, siteName) {
      const template = read(state.templates, templateId);
      if (template.status !== 'Published') throw new Error('Select a published checklist.');
      const item = {
        id: nextId(state.inspections), siteName, templateId, templateName: template.name,
        status: 'Draft', startedAtUtc: new Date().toISOString(), submittedAtUtc: null,
        version: crypto.randomUUID(),
        questions: template.questions.map(q => ({ ...q, answer: null, comment: null })),
      };
      state.inspections.unshift(item);
      save();
      return { id: item.id };
    },
    async answer(id, questionId, { answer, comment, expectedVersion }) {
      const item = latest(id, expectedVersion);
      const question = read(item.questions, questionId);
      if (question.isRequired && answer === 'NotApplicable') throw new Error('Required questions need a Pass or Fail.');
      Object.assign(question, { answer, comment });
      item.version = crypto.randomUUID();
      save();
    },
    async submit(id, expectedVersion) {
      const item = latest(id, expectedVersion);
      if (!canSubmit(item)) throw new Error('Answer every required question before submitting.');
      Object.assign(item, { status: 'Submitted', submittedAtUtc: new Date().toISOString(), version: crypto.randomUUID() });
      save();
    },
  };
}

export function createApiWorkspace(token, organizationId) {
  const headers = { Authorization: `Bearer ${token}` };
  if (organizationId) headers['X-Organization-Id'] = String(organizationId);

  async function request(path, method = 'GET', body) {
    let response;
    try {
      response = await fetch(`/api/${path}`, {
        method,
        headers: body === undefined ? headers : { ...headers, 'Content-Type': 'application/json' },
        body: body === undefined ? undefined : JSON.stringify(body),
      });
    } catch { throw new Error('InspectNow API is unavailable. Check that the API is running.'); }

    const json = response.headers.get('content-type')?.includes('json');
    if (!response.ok) {
      const problem = json ? await response.json() : null;
      if (response.status === 401) throw new Error('The access token was rejected. Reconnect with a valid token.');
      if (response.status === 403) throw new Error('Your account does not have access to this workspace.');
      throw new Error(Object.values(problem?.errors ?? {}).flat()[0] ?? problem?.detail ?? problem?.title ?? `API request failed (${response.status}).`);
    }
    if (response.status === 204) return;
    if (!json) throw new Error('InspectNow API returned an unexpected response. Check the frontend proxy.');
    return response.json();
  }

  async function list(path) {
    const first = await request(`${path}?page=1&pageSize=100`);
    const items = [...first.items];
    for (let page = 2; page <= first.totalPages; page++) {
      items.push(...(await request(`${path}?page=${page}&pageSize=100`)).items);
    }
    return items;
  }

  return {
    organizations: () => request('organizations'),
    templates: () => list('inspection-templates'),
    inspections: () => list('inspections'),
    template: id => request(`inspection-templates/${id}`),
    inspection: id => request(`inspections/${id}`),
    createTemplate: name => request('inspection-templates', 'POST', { name }),
    addQuestion: (id, question) => request(`inspection-templates/${id}/questions`, 'POST', question),
    publish: id => request(`inspection-templates/${id}/publish`, 'POST'),
    start: (templateId, siteName) => request('inspections', 'POST', { templateId, siteName }),
    answer: (id, questionId, input) => request(`inspections/${id}/questions/${questionId}/answer`, 'PUT', input),
    submit: (id, expectedVersion) => request(`inspections/${id}/submit`, 'POST', { expectedVersion }),
  };
}
