import { useCallback, useEffect, useRef, useState } from 'react';
import { AlertCircle, Check, ClipboardList, FileText, LoaderCircle, Plus, X } from 'lucide-react';
import { canSubmit, createApiWorkspace, inspectProgress, statusLabel } from './workspace.js';

export function Badge({ status, label = status }) {
  return <span className={`badge ${status.toLowerCase()}`}><span />{label}</span>;
}

export function Progress({ inspection }) {
  const { total, answered, percent } = inspectProgress(inspection);
  return <div className="progress" aria-label={`${answered} of ${total} questions answered`}>
    <div className="track"><div style={{ width: `${percent}%` }} /></div>
    <span>{answered}/{total}</span>
  </div>;
}

export function Empty({ icon: Icon = ClipboardList, title, children }) {
  return <div className="empty"><span className="empty-icon"><Icon size={22} /></span><strong>{title}</strong>{children && <p>{children}</p>}</div>;
}

export function Feedback({ message }) {
  return message && <div className="error" role="alert"><AlertCircle size={18} />{message}</div>;
}

export function Loading() {
  return <div className="loading" role="status"><LoaderCircle size={18} /> Loading workspace…</div>;
}

export function Modal({ title, description, children, onClose, wide = false }) {
  const dialog = useRef(null);
  useEffect(() => {
    const element = dialog.current;
    element.showModal();
    return () => element.close();
  }, []);

  return <dialog ref={dialog} className={wide ? 'dialog wide' : 'dialog'} aria-labelledby="dialog-title"
    onCancel={event => { event.preventDefault(); onClose(); }}>
    <header className="dialog-header">
      <div><h2 id="dialog-title">{title}</h2>{description && <p>{description}</p>}</div>
      <button className="icon-button" aria-label="Close dialog" onClick={onClose}><X size={21} /></button>
    </header>
    <div className="dialog-body">{children}</div>
  </dialog>;
}

function useAction() {
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState('');
  async function run(action) {
    setBusy(true);
    setError('');
    try { await action(); } catch (problem) { setError(problem.message); } finally { setBusy(false); }
  }
  return { busy, error, run };
}

export function CreateRecord({ kind, workspace, templates, onCreated, onClose }) {
  const isInspection = kind === 'inspection';
  const published = templates.filter(t => t.status === 'Published');
  const [name, setName] = useState('');
  const [template, setTemplate] = useState('');
  const { busy, error, run } = useAction();
  return <form className="form-stack" onSubmit={event => {
    event.preventDefault();
    run(async () => {
      const result = isInspection ? await workspace.start(Number(template), name.trim()) : await workspace.createTemplate(name.trim());
      onCreated(result.id);
    });
  }}>
    <Feedback message={error} />
    {isInspection && <label>Published checklist<select value={template} onChange={e => setTemplate(e.target.value)} required>
      <option value="">Choose a checklist</option>{published.map(t => <option key={t.id} value={t.id}>{t.name}</option>)}
    </select>{!published.length && <small>Publish a checklist before starting an inspection.</small>}</label>}
    <label>{isInspection ? 'Site name' : 'Checklist name'}<input required maxLength={200} value={name}
      placeholder={isInspection ? 'e.g. Riverside Plant' : 'e.g. Weekly safety check'}
      onChange={event => setName(event.target.value)} /></label>
    <div className="dialog-actions"><button type="button" className="button secondary" onClick={onClose}>Cancel</button>
      <button className="button primary" disabled={busy || !name.trim() || (isInspection && !template)}>
        <Plus size={18} /> {isInspection ? 'Start inspection' : 'Create checklist'}</button></div>
  </form>;
}

export function TemplateEditor({ record, workspace, reload }) {
  const [text, setText] = useState('');
  const [required, setRequired] = useState(true);
  const { busy, error, run } = useAction();
  return <div className="form-stack">
    <div className="detail-summary"><Badge status={record.status} /><span>{record.questions.length} questions</span></div>
    <Feedback message={error} />
    {record.questions.length ? <ol className="question-list">{record.questions.map(question => <li key={question.id}>
      <FileText size={17} /><span>{question.text}</span><small>{question.isRequired ? 'Required' : 'Optional'}</small>
    </li>)}</ol> : <Empty icon={FileText} title="Add the first question">Questions will appear in inspections started from this checklist.</Empty>}
    {record.status === 'Draft' && <>
      <form className="form-stack question-form" onSubmit={event => {
        event.preventDefault();
        run(async () => {
          await workspace.addQuestion(record.id, { text: text.trim(), isRequired: required });
          setText('');
          await reload();
        });
      }}>
        <label>New question<input required maxLength={500} value={text} onChange={event => setText(event.target.value)} placeholder="What should the inspector check?" /></label>
        <div className="question-controls"><label className="checkbox"><input type="checkbox" checked={required} onChange={event => setRequired(event.target.checked)} /> Required answer</label>
          <button className="button secondary" disabled={busy || !text.trim()}><Plus size={17} /> Add question</button></div>
      </form>
      <div className="dialog-actions"><span className="muted">Publishing locks this checklist.</span>
        <button className="button primary" disabled={busy || !record.questions.length} onClick={() => run(async () => {
          await workspace.publish(record.id);
          await reload();
        })}><Check size={17} /> Publish checklist</button></div>
    </>}
  </div>;
}

function AnswerForm({ question, disabled, save, markEdited }) {
  const [answer, setAnswer] = useState(question.answer ?? '');
  const [comment, setComment] = useState(question.comment ?? '');
  const edited = answer !== (question.answer ?? '') || comment.trim() !== (question.comment ?? '');
  useEffect(() => markEdited(question.id, edited), [question.id, edited, markEdited]);
  return <form className="answer-card" onSubmit={event => {
    event.preventDefault();
    save({ answer, comment: comment.trim() || null });
  }}>
    <fieldset disabled={disabled}>
      <legend>{question.text}<span>{question.isRequired ? 'Required' : 'Optional'}</span></legend>
      <div className="answer-options">{[['Pass', 'Pass'], ['Fail', 'Fail'], ...(!question.isRequired ? [['NotApplicable', 'N/A']] : [])].map(([value, label]) =>
        <label key={value}><input type="radio" name={`answer-${question.id}`} value={value} checked={answer === value} onChange={() => setAnswer(value)} /> <span>{label}</span></label>)}</div>
      <label className="comment-label">Comment <textarea maxLength={2000} value={comment} onChange={event => setComment(event.target.value)} placeholder="Add an optional note" rows={2} /></label>
      <button className="button secondary small" disabled={!answer || !edited}>Save answer</button>
    </fieldset>
  </form>;
}

export function InspectionEditor({ record, workspace, reload }) {
  const { busy, error, run } = useAction();
  const [editedQuestions, setEditedQuestions] = useState([]);
  const markEdited = useCallback((id, edited) => {
    setEditedQuestions(current => {
      if (current.includes(id) === edited) return current;
      return edited ? [...current, id] : current.filter(value => value !== id);
    });
  }, []);
  const finalized = record.status === 'Submitted';
  const ready = canSubmit(record);
  return <div className="form-stack">
    <div className="detail-summary"><Badge status={record.status} label={statusLabel(record.status)} /><Progress inspection={record} /></div>
    <Feedback message={error} />
    <div className="form-stack">{record.questions.map(question => finalized ? <div className="answer-card result" key={question.id}>
      <strong>{question.text}</strong><span>{question.answer === 'NotApplicable' ? 'N/A' : question.answer ?? 'Unanswered'}</span>
      {question.comment && <p>{question.comment}</p>}
    </div> : <AnswerForm key={question.id} question={question} disabled={busy} markEdited={markEdited} save={input => run(async () => {
      await workspace.answer(record.id, question.id, { ...input, expectedVersion: record.version });
      await reload();
    })} />)}</div>
    {!finalized && <div className="dialog-actions">
      {error && <button className="button secondary" disabled={busy} onClick={() => run(reload)}>Reload inspection</button>}
      <span className="muted">{editedQuestions.length ? 'Save your answer edits before submitting.' : ready ? 'Submission locks all answers.' : 'Save a Pass or Fail for each required question.'}</span>
      <button className="button primary" disabled={busy || !ready || editedQuestions.length > 0} onClick={() => run(async () => {
        await workspace.submit(record.id, record.version);
        await reload();
      })}><Check size={17} /> Submit inspection</button>
    </div>}
  </div>;
}

export function Connect({ connection, onConnect, onDemo }) {
  const [token, setToken] = useState('');
  const [organizations, setOrganizations] = useState([]);
  const [selected, setSelected] = useState('');
  const { busy, error, run } = useAction();
  return <form className="form-stack" onSubmit={event => {
    event.preventDefault();
    run(async () => {
      if (selected) {
        onConnect({ token: token.trim(), organization: organizations.find(org => String(org.id) === selected) });
        return;
      }
      const items = await createApiWorkspace(token.trim()).organizations();
      if (!items.length) throw new Error('This account has no organization memberships.');
      setOrganizations(items);
      setSelected(String(items[0].id));
    });
  }}>
    <Feedback message={error} />
    <label>API access token<input type="password" autoComplete="off" value={token} required placeholder="Paste your access token"
      onChange={event => { setToken(event.target.value); setOrganizations([]); setSelected(''); }} />
      <small>The token stays in memory until you refresh or disconnect.</small></label>
    {!!organizations.length && <label>Organization<select value={selected} onChange={event => setSelected(event.target.value)}>
      {organizations.map(org => <option key={org.id} value={org.id}>{org.name}</option>)}
    </select></label>}
    <div className="dialog-actions"><button type="button" className="button secondary" onClick={onDemo}>
      {connection ? 'Disconnect' : 'Keep using demo'}</button><button className="button primary" disabled={busy || !token.trim()}>
      {busy ? 'Checking…' : selected ? 'Open workspace' : 'Find my organizations'}</button></div>
  </form>;
}
