import { useEffect, useRef, useState } from 'react';
import { DndContext, DragOverlay, PointerSensor, pointerWithin, useDraggable, useDroppable, useSensor, useSensors } from '@dnd-kit/core';
import { ChevronDown, ChevronUp, Copy, Eye, FilePlus2, FileText, GripVertical, Plus, Search, Trash2 } from 'lucide-react';
import { Badge, Empty, Feedback, Loading, WorkPage, useAction } from './components.jsx';
import { fieldTypes, formKinds, formSections, hasAnswer, presetFields, presets } from './forms-data.js';
import { useWorkspaceData } from './team.jsx';
import { recordId, routeLink } from './routes.js';
import { createApiWorkspace } from './workspace.js';

const loadForms = workspace => workspace.forms();
const choice = type => type === 'single' || type === 'multiple';
const newField = (type = 'text') => ({ id: crypto.randomUUID(), type, label: '', required: false, options: [] });
const fieldLimit = 150;
const fieldOptions = field => (field.optionText ?? field.options.join('\n')).split('\n').map(item => item.trim()).filter(Boolean);
const workspaceAudience = { mode: 'workspace', roles: [] };
const builtInRoles = ['Admin', 'Manager', 'Inspector'].map(name => ({ id: name, name }));
const loadRoles = workspace => workspace.roles();
const publicApi = createApiWorkspace();

function AudienceFields({ workspace, value, onChange }) {
  const { data, error } = useWorkspaceData(workspace, loadRoles);
  return <fieldset className="form-question">
    <legend>Who can respond?</legend>
    <label>Audience<select value={value.mode} onChange={event => onChange({ mode: event.target.value, roles: [] })}>
      <option value="workspace">Everyone in this workspace</option>
      <option value="roles">Selected roles</option>
      <option value="public">Anyone with the link</option>
    </select></label>
    {value.mode === 'public' && <small>Anyone with the link can submit without signing in once the form is published.</small>}
    {value.mode === 'workspace' && <small>Workspace members need access to Forms.</small>}
    {value.mode === 'roles' && <>
      <small>Members need access to Forms and at least one selected role.</small>
      <Feedback message={error} />
      {[...builtInRoles, ...(data ?? [])].map(role => <label className="checkbox" key={role.id}>
        <input type="checkbox" checked={value.roles.includes(role.id)} onChange={event => onChange({
          ...value, roles: event.target.checked ? [...value.roles, role.id] : value.roles.filter(id => id !== role.id),
        })} /> {role.name}
      </label>)}
    </>}
  </fieldset>;
}

function PublishedAudience({ form, workspace, onSaved }) {
  const [audience, setAudience] = useState(form.audience ?? workspaceAudience);
  const { busy, error, run } = useAction();
  const [saved, setSaved] = useState(false);
  const link = form.publicKey ? new URL(routeLink('public-form', form.publicKey), location.href).href : null;
  return <details className="panel work-panel">
    <summary>Form audience and sharing</summary>
    <form className="form-stack" onSubmit={event => {
      event.preventDefault();
      run(async () => {
        onSaved(await workspace.updateFormAudience(form.id, form.version, audience));
        setSaved(true);
      });
    }}>
      <AudienceFields workspace={workspace} value={audience} onChange={value => { setAudience(value); setSaved(false); }} />
      <Feedback message={error} />
      <button className="button secondary" disabled={busy || (audience.mode === 'roles' && !audience.roles.length)}>Save audience</button>
      {saved && <p role="status">Audience saved.</p>}
      {link && <label>Public form link<input readOnly value={link} onFocus={event => event.target.select()} /></label>}
      {link && <a href={link} target="_blank" rel="noreferrer">Open public form</a>}
    </form>
  </details>;
}

function DragHandle({ id, data, disabled, label, onKeyboardDrag }) {
  const { attributes, listeners, setNodeRef, isDragging } = useDraggable({ id, data, disabled });
  return <button type="button" id={id} ref={setNodeRef} className={`icon-button builder-grip${isDragging ? ' dragging' : ''}`}
    disabled={disabled} {...attributes} {...listeners} aria-describedby="builder-grip-help" aria-label={label}
    onKeyDown={event => onKeyboardDrag(event, id, data)}><GripVertical size={19} aria-hidden="true" /></button>;
}

function DropGap({ index, dragging, keyboardGap, empty }) {
  const { isOver, setNodeRef } = useDroppable({ id: `gap-${index}`, data: { gap: index } });
  return <div id={`gap-${index}`} ref={setNodeRef} className={`builder-gap${dragging ? ' accepting' : ''}${isOver || keyboardGap === index ? ' over' : ''}${empty ? ' empty-gap' : ''}`}
    aria-hidden="true">{empty && <span>Drop a field here or add one from the palette.</span>}</div>;
}

function BuilderPreview({ form, onClose }) {
  const [answers, setAnswers] = useState({});
  const sections = formSections(form.fields.map((field, index) => ({
    ...field, label: field.label || (field.type === 'section' ? 'Untitled section' : `${fieldTypes[field.type]} ${index + 1}`),
    options: choice(field.type) ? [...new Set(fieldOptions(field))] : [],
  })));
  return <section className="panel builder-preview form-stack">
    <button type="button" className="button secondary" onClick={onClose}>Back to builder</button>
    <h2>{form.name || 'Untitled form'}</h2>
    {sections.length ? sections.map(({ label, fields }) => <section className="form-stack" key={fields[0].id}>
      <h3>{label}</h3>
      {fields.map(field => <Question key={field.id} field={field} value={answers[field.id]}
        onChange={value => setAnswers(current => ({ ...current, [field.id]: value }))} />)}
    </section>) : <p>Add a question to preview the form.</p>}
  </section>;
}

function FormBuilder({ initial, workspace, onSaved, startingKind = 'General' }) {
  const [form, setForm] = useState(initial ?? { name: '', kind: startingKind, fields: [], version: null });
  const [dragging, setDragging] = useState(null);
  const [keyboardDrag, setKeyboardDrag] = useState(null);
  const [preview, setPreview] = useState(false);
  const [status, setStatus] = useState('');
  const newLabel = useRef(null);
  const sensors = useSensors(useSensor(PointerSensor, { activationConstraint: { distance: 6 } }));
  const { busy, error, run } = useAction();
  const set = value => setForm(current => ({ ...current, ...value }));
  const changeField = (id, value) => setForm(current => ({
    ...current, fields: current.fields.map(field => field.id === id ? { ...field, ...value } : field),
  }));
  useEffect(() => {
    if (newLabel.current) {
      document.getElementById(`label-${newLabel.current}`)?.focus();
      newLabel.current = null;
    }
  }, [form.fields]);
  function insert(field, gap, focus = false) {
    if (form.fields.length >= fieldLimit) return;
    if (focus) newLabel.current = field.id;
    set({ fields: form.fields.toSpliced(gap, 0, field) });
    setStatus(`Added ${fieldTypes[field.type]} at position ${gap + 1}.`);
  }
  function move(id, gap) {
    const index = form.fields.findIndex(field => field.id === id);
    if (index < 0 || gap === index || gap === index + 1) return;
    const destination = gap > index ? gap - 1 : gap;
    const field = form.fields[index];
    set({ fields: form.fields.toSpliced(index, 1).toSpliced(destination, 0, field) });
    setStatus(`Moved ${field.label || fieldTypes[field.type]} to position ${destination + 1}.`);
  }
  function drop({ active, over }) {
    setDragging(null);
    if (!over) return;
    const { gap } = over.data.current;
    const { type, fieldId } = active.data.current;
    if (fieldId) move(fieldId, gap);
    else insert(newField(type), gap);
  }
  function keyboardDragField(event, id, data) {
    if (dragging || event.repeat) return;
    if (event.key === ' ' || event.key === 'Enter') {
      event.preventDefault();
      if (keyboardDrag?.id !== id) {
        const gap = data.index ?? form.fields.length;
        setKeyboardDrag({ id, gap });
        setStatus(`Picked up ${data.title}. Insertion point ${gap + 1} of ${form.fields.length + 1}.`);
        document.getElementById(`gap-${gap}`)?.scrollIntoView({ block: 'nearest' });
      } else {
        setKeyboardDrag(null);
        if (data.fieldId) move(data.fieldId, keyboardDrag.gap);
        else insert(newField(data.type), keyboardDrag.gap, true);
      }
      return;
    }
    if (keyboardDrag?.id !== id) return;
    if (event.key === 'Escape') {
      event.preventDefault();
      setKeyboardDrag(null);
      setStatus('Drag canceled.');
      event.currentTarget.scrollIntoView({ block: 'nearest' });
    }
    if (event.key === 'Tab') setKeyboardDrag(null);
    const direction = { ArrowUp: -1, ArrowDown: 1 }[event.key];
    if (!direction) return;
    event.preventDefault();
    let gap = keyboardDrag.gap + direction;
    if (data.index != null && gap === data.index + 1) gap += direction;
    gap = Math.max(0, Math.min(gap, form.fields.length));
    setKeyboardDrag({ id, gap });
    setStatus(`Insertion point ${gap + 1} of ${form.fields.length + 1}.`);
    document.getElementById(`gap-${gap}`)?.scrollIntoView({ block: 'nearest' });
  }
  function save(event) {
    event.preventDefault();
    const publish = event.nativeEvent.submitter?.value === 'publish';
    run(async () => {
      const fields = form.fields.map(field => ({
        ...field, label: field.label.trim(),
        options: choice(field.type) ? fieldOptions(field) : [],
        optionText: undefined,
      }));
      const input = { name: form.name.trim(), kind: form.kind, fields, expectedVersion: form.version,
        audience: workspace.roles ? form.audience ?? workspaceAudience : undefined };
      const saved = form.id ? await workspace.editForm(form.id, input) : await workspace.createForm(input);
      setForm(saved);
      if (publish) await workspace.publishForm(saved.id, saved.version);
      onSaved();
    });
  }
  if (preview) return <BuilderPreview form={form} onClose={() => setPreview(false)} />;
  const atLimit = form.fields.length >= fieldLimit;
  return <DndContext sensors={sensors} collisionDetection={pointerWithin}
    onDragStart={({ active }) => { setKeyboardDrag(null); setDragging(active.data.current.title); }}
    onDragCancel={() => setDragging(null)} onDragEnd={drop}
    accessibility={{
      announcements: {
        onDragStart: ({ active }) => `Picked up ${active.data.current.title}.`,
        onDragOver: ({ over }) => over ? `Insertion point ${over.data.current.gap + 1} of ${form.fields.length + 1}.` : 'Outside the form canvas.',
        onDragEnd: ({ over }) => over ? 'Released on the form canvas.' : 'Drop canceled.',
        onDragCancel: () => 'Drag canceled.',
      },
    }}>
    <div className="builder-layout">
    <span className="builder-status" id="builder-grip-help">Press Space or Enter to pick up and drop. Use Up and Down to choose an insertion point, or Escape to cancel.</span>
    <aside className="panel builder-outline">
      <h2>Field palette</h2>
      <p>Drag a grip onto the canvas, or select a field to add it at the end.</p>
      <div className="builder-palette">{Object.entries(fieldTypes).map(([type, label]) => <div key={type} className="builder-tool">
        <DragHandle id={`palette-${type}`} data={{ type, title: label }} label={`Drag ${label}`} disabled={atLimit} onKeyboardDrag={keyboardDragField} />
        <button type="button" disabled={atLimit} onClick={() => insert(newField(type), form.fields.length, true)}>
          <Plus size={15} aria-hidden="true" /> {label}
        </button>
      </div>)}</div>
      <h2>Form outline</h2>
      {form.fields.length ? <ol>{form.fields.map((field, index) => <li key={field.id}>
        <button type="button" onClick={() => {
          const element = document.getElementById(`field-${field.id}`);
          element?.scrollIntoView({ block: 'center' });
          element?.querySelector('input')?.focus({ preventScroll: true });
        }}>{field.label || `${field.type === 'section' ? 'Section' : 'Question'} ${index + 1}`}</button>
      </li>)}</ol> : <p>Add a section or question to begin.</p>}
    </aside>
    <form className="form-stack form-builder" onSubmit={save}>
    <Feedback message={error} />
    <span className="builder-status" role="status">{status}</span>
    <div className="form-grid">
      <label>Form name<input value={form.name} onChange={event => set({ name: event.target.value })} maxLength={200} placeholder="e.g. Equipment request" required /></label>
      <label>Category<select value={form.kind} onChange={event => set({ kind: event.target.value })}>{formKinds.map(kind => <option key={kind}>{kind}</option>)}</select></label>
    </div>
    {workspace.roles && <AudienceFields workspace={workspace} value={form.audience ?? workspaceAudience}
      onChange={audience => set({ audience })} />}
    {!form.fields.length && presets[form.kind] && <button className="button secondary" type="button"
      onClick={() => set({ name: form.name || presets[form.kind][0], fields: presetFields(form.kind) })}>Use {form.kind.toLowerCase()} starter</button>}
    <div className="builder-toolbar"><h2>Form canvas</h2>
      <button type="button" className="button secondary" onClick={() => setPreview(true)}><Eye size={17} /> Preview</button>
    </div>
    <div className="builder-canvas">
    <DropGap index={0} empty={!form.fields.length} dragging={dragging || keyboardDrag} keyboardGap={keyboardDrag?.gap} />
    {form.fields.map((field, index) => <div key={field.id}>
    <fieldset id={`field-${field.id}`} className={`builder-field${field.type === 'section' ? ' builder-section' : ''}`}>
      <legend>{field.type === 'section' ? 'Section' : 'Question'} {index + 1}</legend>
      <div className="builder-field-heading">
        <DragHandle id={`field-drag-${field.id}`} data={{ fieldId: field.id, index, title: field.label || fieldTypes[field.type] }}
          label={`Drag field ${index + 1}: ${field.label || fieldTypes[field.type]}`} onKeyboardDrag={keyboardDragField} />
        <span>{fieldTypes[field.type]}</span>
      </div>
      <div className="form-grid">
        <label>Label<input id={`label-${field.id}`} value={field.label} onChange={event => changeField(field.id, { label: event.target.value })} maxLength={240} required /></label>
        <label>Type<select value={field.type} onChange={event => changeField(field.id, {
          type: event.target.value, required: event.target.value === 'section' ? false : field.required,
        })}>{Object.entries(fieldTypes).map(([type, label]) => <option key={type} value={type}>{label}</option>)}</select></label>
      </div>
      {choice(field.type) && <label>Options <textarea rows={3} required maxLength={2500}
        value={field.optionText ?? field.options.join('\n')} placeholder="Enter one option per line"
        onChange={event => changeField(field.id, { optionText: event.target.value })} /></label>}
      <div className="builder-actions">
        {field.type !== 'section' && <label className="checkbox"><input type="checkbox" checked={field.required}
          onChange={event => changeField(field.id, { required: event.target.checked })} /> Required</label>}
        <div><button type="button" className="icon-button" aria-label={`Move field ${index + 1} up`} disabled={!index} onClick={() => move(field.id, index - 1)}><ChevronUp size={18} /></button>
          <button type="button" className="icon-button" aria-label={`Move field ${index + 1} down`} disabled={index === form.fields.length - 1} onClick={() => move(field.id, index + 2)}><ChevronDown size={18} /></button>
          <button type="button" className="icon-button" aria-label={`Duplicate field ${index + 1}`} disabled={atLimit}
            onClick={() => insert({ ...field, id: crypto.randomUUID() }, index + 1, true)}><Copy size={17} /></button>
          <button type="button" className="icon-button" aria-label={`Remove field ${index + 1}`}
            onClick={() => set({ fields: form.fields.filter(item => item.id !== field.id) })}><Trash2 size={17} /></button></div>
      </div>
    </fieldset>
    <DropGap index={index + 1} dragging={dragging || keyboardDrag} keyboardGap={keyboardDrag?.gap} />
    </div>)}
    </div>
    {atLimit && <p className="muted">This form has reached the 150-field limit.</p>}
    <div className="dialog-actions">
      <span className="muted">Publishing locks the question structure.</span>
      <button className="button secondary" disabled={busy || (form.audience?.mode === 'roles' && !form.audience.roles.length)}>Save draft</button>
      <button className="button primary" value="publish" disabled={busy || (form.audience?.mode === 'roles' && !form.audience.roles.length) || !form.fields.some(field => field.type !== 'section')}>Publish form</button>
    </div>
    </form>
  </div>
  <DragOverlay dropAnimation={null}>{dragging && <div className="builder-drag-overlay"><GripVertical size={18} /> {dragging}</div>}</DragOverlay>
  </DndContext>;
}

function Question({ field, value, onChange }) {
  const name = field.id;
  const suffix = field.required ? <span className="required-mark" aria-label="required"> *</span> : null;
  if (choice(field.type) || field.type === 'boolean' || field.type === 'rating') {
    const options = field.type === 'boolean' ? [[true, 'Yes'], [false, 'No']]
      : field.type === 'rating' ? [1, 2, 3, 4, 5].map(number => [number, number]) : field.options.map(option => [option, option]);
    const multiple = field.type === 'multiple';
    const selected = Array.isArray(value) ? value : [];
    return <fieldset className="form-question"><legend>{field.label}{suffix}</legend>
      <div className={field.type === 'rating' ? 'choice-options rating-options' : 'choice-options'}>{options.map(([option, label]) => {
        const checked = multiple ? selected.includes(option) : value === option;
        return <label key={String(option)}><input type={multiple ? 'checkbox' : 'radio'} name={name} checked={checked}
          onChange={() => onChange(multiple ? checked ? selected.filter(item => item !== option) : [...selected, option] : option)} /> {label}</label>;
      })}</div>
    </fieldset>;
  }
  return <label className="form-question"><span>{field.label}{suffix}</span>
    {field.type === 'paragraph' ? <textarea value={value ?? ''} rows={4} maxLength={4000} onChange={event => onChange(event.target.value)} />
      : <input type={field.type} value={value ?? ''} step={field.type === 'number' ? 'any' : undefined}
        maxLength={field.type === 'email' ? 254 : 500}
        onChange={event => onChange(field.type === 'number' && event.target.value !== '' ? event.target.valueAsNumber : event.target.value)} />}
  </label>;
}

function FormRunner({ form, workspace, publicKey }) {
  const sectionHeading = useRef(null);
  const [draft, setDraft] = useState(publicKey ? { answers: {} } : null);
  const [page, setPage] = useState(0);
  const [message, setMessage] = useState('');
  const [loadingError, setLoadingError] = useState('');
  const [submitted, setSubmitted] = useState(false);
  const { busy, error, run } = useAction();
  useEffect(() => {
    if (publicKey) return;
    let active = true;
    workspace.formDraft(form.id).then(value => { if (active) setDraft(value); },
      problem => { if (active) setLoadingError(problem.message); });
    return () => { active = false; };
  }, [form.id, workspace, publicKey]);
  useEffect(() => {
    if (!sectionHeading.current) return;
    sectionHeading.current.focus({ preventScroll: true });
    sectionHeading.current.scrollIntoView({ block: 'start' });
  }, [page]);
  if (!draft) return loadingError ? <Feedback message={loadingError} /> : <Loading />;
  if (submitted) return <Empty icon={FileText} title="Response submitted">Your answers are saved in the response library.</Empty>;

  const sections = formSections(form.fields);
  const active = sections[page];
  const questions = sections.flatMap(section => section.fields);
  const answered = questions.filter(field => hasAnswer(draft.answers[field.id])).length;
  const last = page === sections.length - 1;
  const save = (advance, submit) => run(async () => {
    if (submit || advance) {
      const missing = (submit ? questions : active.fields).find(field => field.required && !hasAnswer(draft.answers[field.id]));
      if (missing) {
        setPage(sections.findIndex(section => section.fields.includes(missing)));
        throw new Error(`Answer ${missing.label} before continuing.`);
      }
    }
    setMessage('');
    if (publicKey) {
      if (submit) {
        await workspace.submitPublicForm(publicKey, draft.answers);
        setSubmitted(true);
      } else setPage(current => current + 1);
      return;
    }
    let version = draft.version;
    if (submit && !version) {
      const saved = await workspace.saveFormDraft(form.id, { answers: draft.answers, expectedVersion: null, submit: false });
      setDraft(saved);
      version = saved.version;
    }
    const saved = await workspace.saveFormDraft(form.id, { answers: draft.answers, expectedVersion: version, submit });
    setDraft(saved);
    if (submit) setSubmitted(true);
    else if (advance) setPage(current => current + 1);
    else setMessage('Progress saved.');
  });
  return <form className="form-stack form-runner" onSubmit={event => { event.preventDefault(); save(!last, last); }}>
    <div className="runner-progress">
      <span>Section {page + 1} of {sections.length}</span><span>{answered} of {questions.length} answered</span>
      <progress value={answered} max={questions.length} aria-label="Questions answered" />
    </div>
    <h2 ref={sectionHeading} tabIndex={-1}>{active.label}</h2>
    <Feedback message={error} />
    <fieldset className="runner-fields" disabled={busy}>
      {active.fields.map(field => <Question key={field.id} field={field} value={draft.answers[field.id]} onChange={value => {
        setMessage('');
        setDraft(current => ({ ...current, answers: { ...current.answers, [field.id]: value } }));
      }} />)}
    </fieldset>
    {message && <p role="status" className="muted">{message}</p>}
    <div className="dialog-actions runner-actions">
      {!!page && <button className="button secondary" type="button" disabled={busy} onClick={() => setPage(current => current - 1)}>Back</button>}
      {!publicKey && <button className="button secondary" type="button" disabled={busy} onClick={() => save(false, false)}>Save progress</button>}
      <button className="button primary" disabled={busy}>{last ? 'Submit response' : publicKey ? 'Continue' : 'Save and continue'}</button>
    </div>
  </form>;
}

export function PublicFormPage({ publicKey }) {
  const [form, setForm] = useState(null);
  const [error, setError] = useState('');
  useEffect(() => {
    let active = true;
    publicApi.publicForm(publicKey).then(value => { if (active) setForm(value); },
      problem => { if (active) setError(problem.message); });
    return () => { active = false; };
  }, [publicKey]);
  return <main className="public-form-page">
    <WorkPage title={form?.name ?? 'Shared form'} description={form?.kind} layout="reading">
      <Feedback message={error} />
      {form ? <div className="panel work-panel"><FormRunner form={form} workspace={publicApi} publicKey={publicKey} /></div> : !error && <Loading />}
    </WorkPage>
  </main>;
}

function OpenForm({ id, workspace, back, onSaved, canManage }) {
  const [form, setForm] = useState(null);
  const [error, setError] = useState('');
  useEffect(() => {
    let active = true;
    workspace.form(id).then(value => { if (active) setForm(value); }, problem => { if (active) setError(problem.message); });
    return () => { active = false; };
  }, [id, workspace]);
  return <WorkPage title={form?.name ?? 'Open form'} description={form?.kind} back={back} backLabel="Back to forms"
    layout={form?.publishedAtUtc ? 'reading' : 'wide'}>
    <Feedback message={error} />
    {form ? form.publishedAtUtc ? <>
      {canManage && workspace.updateFormAudience && <PublishedAudience form={form} workspace={workspace} onSaved={setForm} />}
      {form.canRespond === false ? <Empty title="Your roles do not match this form’s audience" /> :
        <div className="panel work-panel"><FormRunner form={form} workspace={workspace} /></div>}
    </> :
      canManage ? <FormBuilder initial={form} workspace={workspace} onSaved={onSaved} /> :
        <Empty title="This form is still a draft" /> : !error && <Loading />}
  </WorkPage>;
}

function ResponseDetails({ workspace, id, back }) {
  const [detail, setDetail] = useState(null);
  const [error, setError] = useState('');
  useEffect(() => {
    let active = true;
    workspace.formResponse(id).then(value => { if (active) setDetail(value); }, problem => { if (active) setError(problem.message); });
    return () => { active = false; };
  }, [id, workspace]);
  return <WorkPage title={detail?.form.name ?? 'Submitted response'} back={back} backLabel="Back to response library" layout="reading"
    description={detail ? `Response #${detail.response.id} · ${new Date(detail.response.submittedAtUtc).toLocaleString()}` : undefined}>
    <Feedback message={error} />
    {detail ? formSections(detail.form.fields).map((section, index) => <section className="panel work-panel response-section" key={index}>
      <h2>{section.label}</h2><dl className="response-details">{section.fields.map(field => {
        const value = detail.response.answers[field.id];
        return <div key={field.id}><dt>{field.label}</dt><dd>{Array.isArray(value) && value.length ? value.join(', ') :
          typeof value === 'boolean' ? value ? 'Yes' : 'No' : hasAnswer(value) ? String(value) : 'Unanswered'}</dd></div>;
      })}</dl>
    </section>) : !error && <Loading />}
  </WorkPage>;
}

function ResponseLibrary({ workspace, forms, base, query }) {
  const params = new URLSearchParams(query);
  const formId = recordId(params.get('form'));
  const page = Math.min(1_000_000, recordId(params.get('page')) ?? 1);
  const url = (targetPage, targetForm) => {
    const next = new URLSearchParams();
    if (targetForm) next.set('form', targetForm);
    if (targetPage > 1) next.set('page', targetPage);
    return `${base}${next.size ? `?${next}` : ''}`;
  };
  const [data, setData] = useState(null);
  const [error, setError] = useState('');
  useEffect(() => {
    let active = true;
    setData(null);
    setError('');
    workspace.formResponses(page, formId).then(result => {
      if (active) { setData(result); setError(''); }
    }, problem => { if (active) setError(problem.message); });
    return () => { active = false; };
  }, [workspace, formId, page]);
  return <section className="panel team-panel">
    <div className="team-heading"><h2>Submitted responses</h2>
      <select aria-label="Filter by form" value={formId ?? ''} onChange={event => { location.hash = url(1, event.target.value); }}>
        <option value="">All forms</option>{forms.filter(form => form.publishedAtUtc).map(form => <option key={form.id} value={form.id}>{form.name}</option>)}
      </select>
    </div>
    <Feedback message={error} />
    {data?.items.length ? <>
      <div className="assignment-list">{data.items.map(response => <a key={response.id} className="response-row" href={`${base}/${response.id}${query ? `?${query}` : ''}`}>
        <span><strong>{response.name}</strong><small>{response.kind}</small></span>
        <span>{new Date(response.submittedAtUtc).toLocaleString()}</span>
      </a>)}</div>
      <div className="team-heading"><span className="muted">{data.total} responses</span>
        <div className="question-controls">
          <button className="button secondary small" disabled={page === 1} onClick={() => { location.hash = url(page - 1, formId); }}>Previous</button>
          <button className="button secondary small" disabled={page * data.size >= data.total} onClick={() => { location.hash = url(page + 1, formId); }}>Next</button>
        </div>
      </div>
    </> : data ? <Empty title="No submitted responses">Completed responses will appear here.</Empty> : !error && <Loading />}
  </section>;
}

export function FormsPage({ workspace, canManage, startingKind = '', page, parts, routeQuery }) {
  const [target, idPart] = parts;
  const tab = target === 'responses' && canManage ? 'responses' : 'forms';
  const [query, setQuery] = useState('');
  const [kind, setKind] = useState(startingKind);
  const [revision, setRevision] = useState(0);
  const { data, error } = useWorkspaceData(workspace, loadForms, revision);
  const visible = data?.filter(form => (!kind || form.kind === kind) && form.name.toLowerCase().includes(query.trim().toLowerCase()));
  const back = routeLink(page);
  const responseBase = routeLink(page, 'responses');
  const saved = () => { location.hash = back; setRevision(current => current + 1); };
  const id = recordId(idPart);
  if (target === 'new' && canManage) return <WorkPage title="Create a form" description="Add sections and questions, then save a draft or publish."
    back={back} backLabel="Back to forms" layout="wide"><FormBuilder workspace={workspace} onSaved={saved} startingKind={startingKind || 'General'} /></WorkPage>;
  if (target === 'view' && id) return <OpenForm key={id} id={id} workspace={workspace} back={back} onSaved={saved} canManage={canManage} />;
  if (target === 'responses' && id && canManage)
    return <ResponseDetails key={id} workspace={workspace} id={id} back={`${responseBase}${routeQuery ? `?${routeQuery}` : ''}`} />;
  return <div className="form-workspace">
    <div className="team-heading">
      <div className="form-tabs">
        <a className={`button ${tab === 'forms' ? 'primary' : 'secondary'}`} aria-current={tab === 'forms' ? 'page' : undefined} href={back}>Forms</a>
        {canManage && <a className={`button ${tab === 'responses' ? 'primary' : 'secondary'}`} aria-current={tab === 'responses' ? 'page' : undefined} href={responseBase}>Response library</a>}
      </div>
      {canManage && <a className="button primary" href={routeLink(page, 'new')}><Plus size={18} /> New form</a>}
    </div>
    <Feedback message={error} />
    {tab === 'responses' ? <ResponseLibrary workspace={workspace} forms={data ?? []} base={responseBase} query={routeQuery} /> : <section className="panel team-panel">
      <div className="team-heading"><h2>Form library</h2><span className="muted">{data?.length ?? 0} forms</span></div>
      <div className="form-filters">
        <label className="search"><Search size={18} /><input aria-label="Search forms" placeholder="Search forms…" value={query} onChange={event => setQuery(event.target.value)} /></label>
        <select aria-label="Filter categories" value={kind} onChange={event => setKind(event.target.value)}><option value="">All categories</option>
          {formKinds.map(item => <option key={item}>{item}</option>)}</select>
      </div>
      {visible?.length ? <div className="form-library-grid">{visible.map(form => <a className="checklist-card" key={form.id} href={routeLink(page, 'view', form.id)}>
        <div><span className="stat-icon green"><FileText size={20} /></span><Badge status={form.publishedAtUtc ? 'Published' : 'Draft'} /></div>
        <strong>{form.name}</strong><small>{form.kind}</small><span className="card-action">{form.publishedAtUtc ? 'Fill form' : 'Edit draft'}</span>
      </a>)}</div> : visible ? <Empty icon={FilePlus2} title={data.length ? 'No matching forms' : 'No forms yet'}>
        {data.length ? 'Try another search or category.' : canManage ? 'Create a form to get started.' : 'Published forms will appear here.'}
      </Empty> : !error && <Loading />}
    </section>}
  </div>;
}
