export const formKinds = ['General', 'Survey', 'Inspection', 'Assessment', 'Intake', 'Request', 'Audit'];
export const fieldTypes = {
  section: 'Section heading', text: 'Short text', paragraph: 'Long text', email: 'Email', number: 'Number',
  date: 'Date', time: 'Time', single: 'Single choice', multiple: 'Multiple choice', boolean: 'Yes / No', rating: 'Rating',
};
export const presets = {
  Survey: ['Customer feedback', [['section', 'Your experience'], ['rating', 'How satisfied were you?'], ['paragraph', 'What could we improve?']]],
  Inspection: ['Site walkthrough', [['section', 'Safety checks'], ['single', 'Are exits clear?', ['Pass', 'Fail', 'Not applicable']], ['paragraph', 'Describe any issues.']]],
  Assessment: ['Training knowledge check', [['section', 'Knowledge check'], ['single', 'Who should you report a hazard to?', ['Your supervisor', 'A visitor', 'Nobody']], ['paragraph', 'Explain what you would do next.']]],
  Intake: ['New client intake', [['section', 'Contact details'], ['text', 'Full name'], ['email', 'Email address'], ['date', 'Preferred appointment date']]],
  Request: ['Maintenance request', [['section', 'Request details'], ['text', 'Location'], ['single', 'Priority', ['Routine', 'Urgent']], ['paragraph', 'Describe the issue.']]],
  Audit: ['Process compliance review', [['section', 'Controls'], ['boolean', 'Is the control documented?'], ['multiple', 'What evidence was reviewed?', ['Policy', 'Training record', 'Audit log']], ['paragraph', 'Record any exceptions.']]],
};

export function presetFields(kind) {
  return (presets[kind]?.[1] ?? []).map(([type, label, options]) => ({
    id: crypto.randomUUID(), type, label, required: type !== 'section' && type !== 'paragraph', options: options ?? [],
  }));
}

export function formSections(fields) {
  const sections = [{ label: 'Questions', fields: [] }];
  for (const field of fields) {
    if (field.type === 'section') sections.push({ label: field.label, fields: [] });
    else sections.at(-1).fields.push(field);
  }
  return sections.filter(section => section.fields.length);
}

export function hasAnswer(value) {
  return value != null && (typeof value !== 'string' || Boolean(value.trim())) && (!Array.isArray(value) || value.length > 0);
}

export function createDemoForms(storage) {
  const key = 'inspectnow-demo-forms-v1';
  let state;
  try { state = JSON.parse(storage.getItem(key)); } catch { /* Start with examples when the saved demo is invalid. */ }
  state ??= {
    forms: Object.entries(presets).map(([kind, [name]], index) => ({
      id: index + 1, kind, name, fields: presetFields(kind), version: crypto.randomUUID(), publishedAtUtc: new Date().toISOString(),
    })),
    drafts: {}, responses: [],
  };
  const persist = () => storage.setItem(key, JSON.stringify(state));
  const find = id => {
    const form = state.forms.find(item => item.id === id);
    if (!form) throw new Error('Form not found.');
    return form;
  };
  function saveForm(input, form) {
    if (form.publishedAtUtc || form.version !== input.expectedVersion) throw new Error('This form changed. Reopen it and try again.');
    const { expectedVersion, ...data } = input;
    Object.assign(form, structuredClone(data), { version: crypto.randomUUID() });
    persist();
    return structuredClone(form);
  }
  return {
    async forms() { return structuredClone(state.forms); },
    async form(id) { return structuredClone(find(id)); },
    async createForm(input) {
      const form = { id: Math.max(0, ...state.forms.map(item => item.id)) + 1, version: null, publishedAtUtc: null };
      state.forms.push(form);
      return saveForm({ ...input, expectedVersion: null }, form);
    },
    async editForm(id, input) { return saveForm(input, find(id)); },
    async publishForm(id, expectedVersion) {
      const form = find(id);
      if (form.publishedAtUtc || form.version !== expectedVersion) throw new Error('This form changed. Reopen it and try again.');
      if (!form.fields.some(field => field.type !== 'section')) throw new Error('Add a question before publishing.');
      Object.assign(form, { publishedAtUtc: new Date().toISOString(), version: crypto.randomUUID() });
      persist();
      return structuredClone(form);
    },
    async formDraft(id) { return structuredClone(state.drafts[id] ?? { answers: {}, version: null }); },
    async saveFormDraft(id, { answers, expectedVersion, submit }) {
      const form = find(id);
      const version = state.drafts[id]?.version ?? null;
      if (!form.publishedAtUtc || expectedVersion !== version || (submit && !version)) throw new Error('This response changed. Reopen it and try again.');
      const draft = { answers: structuredClone(answers), version: crypto.randomUUID() };
      if (submit) {
        const missing = form.fields.find(field => field.required && !hasAnswer(answers[field.id]));
        if (missing) throw new Error(`Answer ${missing.label} before submitting.`);
        state.responses.unshift({ ...draft, id: (state.responses[0]?.id ?? 0) + 1, formId: id, name: form.name, kind: form.kind, submittedAtUtc: new Date().toISOString() });
        delete state.drafts[id];
      } else state.drafts[id] = draft;
      persist();
      return structuredClone(draft);
    },
    async formResponses(page, formId) {
      const items = state.responses.filter(item => !formId || item.formId === formId);
      return { items: items.slice((page - 1) * 25, page * 25), total: items.length, page, size: 25 };
    },
    async formResponse(id) {
      const response = state.responses.find(item => item.id === id);
      if (!response) throw new Error('Response not found.');
      return { response, form: find(response.formId) };
    },
  };
}
