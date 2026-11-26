import { useEffect, useState } from 'react';
import { Building2, ClipboardList, Plus, Users } from 'lucide-react';
import { Badge, Empty, Feedback, Loading, Modal, useAction } from './components.jsx';
import { statusLabel } from './workspace.js';

export const workspaceTypes = {
  General: 'General business',
  FieldOperations: 'Field operations',
  Education: 'Education & training',
  ClientServices: 'Client services',
};

export function AccountForm({ api, onSignedIn }) {
  const [register, setRegister] = useState(false);
  const { busy, error, run } = useAction();
  return <form className="form-stack" onSubmit={event => {
    event.preventDefault();
    const { email, password } = Object.fromEntries(new FormData(event.currentTarget));
    run(async () => { await api.signIn(register ? 'register' : 'login', { email, password }); await onSignedIn(); });
  }}>
    <Feedback message={error} />
    <label>Email<input type="email" name="email" autoComplete="username" maxLength={254} required /></label>
    <label>Password<input type="password" name="password" autoComplete={register ? 'new-password' : 'current-password'}
      minLength={register ? 12 : undefined} maxLength={128} required />
      {register && <small>Use at least 12 characters, with uppercase, lowercase, a number and a symbol.</small>}</label>
    <div className="dialog-actions"><button className="button secondary" type="button" disabled={busy} onClick={() => setRegister(!register)}>
      {register ? 'Use an existing account' : 'Create an account'}</button>
      <button className="button primary" disabled={busy}>{register ? 'Register' : 'Sign in'}</button></div>
  </form>;
}

export function WorkspacePicker({ api, account, onSelect, onSignOut }) {
  const [mode, setMode] = useState(account.canProvision ? 'create' : 'join');
  const [value, setValue] = useState('');
  const [type, setType] = useState('General');
  const { busy, error, run } = useAction();
  return <div className="form-stack">
    <div className="detail-summary"><Users size={18} /> <span>{account.email}</span></div>
    <Feedback message={error} />
    {account.organizations.length > 0 && <div className="form-stack">
      {account.organizations.map(org => <button key={org.id} className="button secondary workspace-choice" onClick={() => onSelect(org)}>
        <Building2 size={17} /> <strong>{org.name}<small>{workspaceTypes[org.type] ?? workspaceTypes.General}</small></strong><span>{org.role}</span>
      </button>)}
    </div>}
    <div className="question-controls">
      {account.canProvision && <button type="button" className={mode === 'create' ? 'button primary small' : 'button secondary small'} onClick={() => { setMode('create'); setValue(''); }}>Create workspace</button>}
      <button type="button" className={mode === 'join' ? 'button primary small' : 'button secondary small'} onClick={() => { setMode('join'); setValue(''); }}>Join workspace</button>
    </div>
    <form className="form-stack" onSubmit={event => {
      event.preventDefault();
      run(async () => {
        const result = mode === 'create' ? await api.createOrganization(value.trim(), type) : await api.joinOrganization(value.trim());
        const organizations = await api.organizations();
        onSelect(organizations.find(org => org.id === (result.id ?? result.organizationId)), organizations);
      });
    }}>
      <label>{mode === 'create' ? 'Workspace name' : 'Invitation code'}<input value={value} onChange={event => setValue(event.target.value)}
        required maxLength={mode === 'create' ? 200 : 64} minLength={mode === 'join' ? 64 : undefined}
        placeholder={mode === 'create' ? 'e.g. Northside College' : 'Paste the invitation code'} /></label>
      {mode === 'create' && <label>Organization type<select value={type} onChange={event => setType(event.target.value)}>
        {Object.entries(workspaceTypes).map(([id, label]) => <option key={id} value={id}>{label}</option>)}
      </select></label>}
      <div className="dialog-actions"><button className="button secondary" type="button" disabled={busy} onClick={() => run(onSignOut)}>Sign out</button>
        <button className="button primary" disabled={busy || !value.trim()}>{mode === 'create' ? 'Create workspace' : 'Join workspace'}</button></div>
    </form>
  </div>;
}

export function useWorkspaceData(workspace, load, revision) {
  const [data, setData] = useState(null);
  const [error, setError] = useState('');
  useEffect(() => {
    let active = true;
    load(workspace).then(result => { if (active) { setData(result); setError(''); } },
      problem => { if (active) setError(problem.message); });
    return () => { active = false; };
  }, [workspace, load, revision]);
  return { data, error };
}

const loadMembers = workspace => workspace.members();
const loadRoles = workspace => workspace.roles();
const loadSites = workspace => workspace.sites();
const loadAssignments = workspace => workspace.assignments();

export function TeamPage({ workspace, canInvite, canInviteAdmin, organization }) {
  const [revision, setRevision] = useState(0);
  const { data, error } = useWorkspaceData(workspace, loadMembers, revision);
  const { data: roles, error: rolesError } = useWorkspaceData(workspace, loadRoles, revision);
  const { busy, error: actionError, run } = useAction();
  const [role, setRole] = useState('Inspector');
  const [invitation, setInvitation] = useState(null);
  const [selected, setSelected] = useState(null);
  const [roleMember, setRoleMember] = useState(null);
  return <section className="panel team-panel">
    <h2>Workspace members</h2>
    <Feedback message={error || rolesError || actionError} />
    {data ? <ul className="team-list">{data.map(member => <li key={member.id}><Users size={18} /><span>{member.email}
      {roles?.filter(role => member.customRoleIds?.includes(role.id)).map(role => <small key={role.id} className="member-custom-role">{role.name}</small>)}</span><strong>{member.role}</strong>
      {canInvite && member.role !== 'Admin' && <button className="button secondary small" onClick={() => setSelected(member)}>Edit access</button>}
      {canInvite && roles?.length > 0 && <button className="button secondary small" onClick={() => setRoleMember(member)}>Assign roles</button>}</li>)}</ul>
      : !error && <Loading />}
    {canInvite && <form className="form-stack" onSubmit={event => {
      event.preventDefault();
      run(async () => setInvitation(await workspace.invite(role)));
    }}>
      <label>Invite as<select value={role} onChange={e => setRole(e.target.value)}><option>Inspector</option><option>Manager</option>{canInviteAdmin && <option>Admin</option>}</select></label>
      <button className="button primary" disabled={busy}>Create invitation</button>
      {invitation && <label>One-time invitation code<input readOnly value={invitation.code} onFocus={e => e.target.select()} />
        <small>Share privately with your teammate. It expires in 48 hours.</small></label>}
    </form>}
    <section className="form-stack">
      <h2>Custom roles</h2>
      <p className="muted">Assign roles to members to control who can respond to selected forms.</p>
      {roles?.length > 0 && <ul className="team-list">{roles.map(role => <li key={role.id}>{role.name}</li>)}</ul>}
      {canInvite && <form className="form-stack" onSubmit={event => {
        event.preventDefault();
        const form = event.currentTarget;
        const name = new FormData(form).get('name').trim();
        run(async () => { await workspace.createRole(name); form.reset(); setRevision(current => current + 1); });
      }}>
        <label>Role name<input name="name" required maxLength={80} placeholder="e.g. Quality reviewer" /></label>
        <button className="button secondary" disabled={busy}>Create role</button>
      </form>}
    </section>
    {roleMember && <AssignRoles workspace={workspace} member={roleMember} roles={roles}
      onClose={() => setRoleMember(null)} onSaved={() => { setRoleMember(null); setRevision(current => current + 1); }} />}
    {selected && <MemberAccess workspace={workspace} member={selected} organization={organization} onClose={() => setSelected(null)}
      onSaved={() => { setSelected(null); setRevision(current => current + 1); }} />}
  </section>;
}

function AssignRoles({ workspace, member, roles, onClose, onSaved }) {
  const { busy, error, run } = useAction();
  return <Modal title="Assign custom roles" description={member.email} onClose={onClose}>
    <form className="form-stack" onSubmit={event => {
      event.preventDefault();
      const ids = new FormData(event.currentTarget).getAll('roles');
      run(async () => { await workspace.assignRoles(member.id, ids); onSaved(); });
    }}>
      <Feedback message={error} />
      <fieldset className="form-question"><legend>Custom roles</legend>
        {roles.map(role => <label className="checkbox" key={role.id}>
          <input type="checkbox" name="roles" value={role.id} defaultChecked={member.customRoleIds?.includes(role.id)} /> {role.name}
        </label>)}
      </fieldset>
      <button className="button primary" disabled={busy}>Save roles</button>
    </form>
  </Modal>;
}

function MemberAccess({ workspace, member, organization, onClose, onSaved }) {
  const { busy, error, run } = useAction();
  return <Modal title="Edit workspace access" description={member.email} onClose={onClose}>
    <form className="form-stack" onSubmit={event => {
      event.preventDefault();
      const fields = new FormData(event.currentTarget);
      run(async () => {
        await workspace.updateMemberAccess(member.id, {
          role: fields.get('role'), canUseForms: fields.has('forms'), canUseInspections: fields.has('inspections'),
        });
        onSaved();
      });
    }}>
      <Feedback message={error} />
      <label>Role<select name="role" defaultValue={member.role}><option>Manager</option><option>Inspector</option></select></label>
      {organization.offersForms && <label className="checkbox"><input type="checkbox" name="forms" defaultChecked={member.canUseForms} /> Forms and responses</label>}
      {organization.offersInspections && <label className="checkbox"><input type="checkbox" name="inspections" defaultChecked={member.canUseInspections} /> Inspections and corrective actions</label>}
      <div className="dialog-actions"><button className="button primary" disabled={busy}>Save access</button></div>
    </form>
  </Modal>;
}

export function SitesPage({ workspace }) {
  const [revision, setRevision] = useState(0);
  const { data, error } = useWorkspaceData(workspace, loadSites, revision);
  const { busy, error: actionError, run } = useAction();
  const [name, setName] = useState('');
  return <section className="panel team-panel">
    <h2>Inspection sites</h2>
    <Feedback message={error || actionError} />
    <form className="form-stack" onSubmit={event => {
      event.preventDefault();
      run(async () => { await workspace.createSite(name.trim()); setName(''); setRevision(value => value + 1); });
    }}>
      <label>Site name<input required maxLength={200} value={name} onChange={event => setName(event.target.value)} placeholder="e.g. Main campus" /></label>
      <button className="button primary" disabled={busy || !name.trim()}><Plus size={17} /> Add site</button>
    </form>
    {data?.length ? <ul className="team-list">{data.map(site => <li key={site.id}><Building2 size={18} /><span>{site.name}</span></li>)}</ul>
      : data ? <Empty icon={Building2} title="No sites yet">Add a site to use it in an assignment.</Empty> : !error && <Loading />}
  </section>;
}

export function AssignmentsPage({ workspace, canManage, openRecord, newAssignment, revision }) {
  const { data, error } = useWorkspaceData(workspace, loadAssignments, revision);
  return <section className="panel team-panel">
    <div className="team-heading"><h2>{canManage ? 'Workspace assignments' : 'Assigned to you'}</h2>
      {canManage && <button className="button primary" onClick={newAssignment}><Plus size={17} /> New assignment</button>}</div>
    <Feedback message={error} />
    {data?.length ? <div className="assignment-list">{data.map(item => <button key={item.id} className="assignment-item" onClick={() => openRecord(item.id)}>
      <span className="row-icon"><ClipboardList size={19} /></span><span><strong>{item.siteName}</strong><small>{item.templateName}</small></span>
      {canManage && <span className="muted">{item.assignee}</span>}
      <span>Due {new Date(item.dueDate + 'T00:00:00').toLocaleDateString()}</span>
      <Badge status={item.status} label={statusLabel(item.status)} />
    </button>)}</div> : data ? <Empty title="No assignments yet">{canManage ? 'Assign a published checklist to an inspector.' : 'New assignments will appear here.'}</Empty>
      : !error && <Loading />}
  </section>;
}

const loadAssignmentOptions = async workspace => {
  const [sites, members] = await Promise.all([workspace.sites(), workspace.members()]);
  return { sites, inspectors: members.filter(member => member.role === 'Inspector' && member.canUseInspections !== false) };
};

export function AssignmentForm({ workspace, templates, onCreated }) {
  const { data, error } = useWorkspaceData(workspace, loadAssignmentOptions);
  const { busy, error: actionError, run } = useAction();
  const published = templates.filter(item => item.status === 'Published');
  if (!data) return error ? <Feedback message={error} /> : <Loading />;
  if (!data.sites.length || !data.inspectors.length || !published.length)
    return <Empty title="Set up assignment options">Add a site, publish a checklist, and invite an inspector before creating an assignment.</Empty>;
  return <form className="form-stack" onSubmit={event => {
    event.preventDefault();
    const values = Object.fromEntries(new FormData(event.currentTarget));
    run(async () => onCreated((await workspace.createAssignment({
      ...values, siteId: Number(values.siteId), templateId: Number(values.templateId),
    })).id));
  }}>
    <Feedback message={actionError} />
    <label>Site<select name="siteId" required><option value="">Choose a site</option>{data.sites.map(site => <option key={site.id} value={site.id}>{site.name}</option>)}</select></label>
    <label>Checklist<select name="templateId" required><option value="">Choose a checklist</option>{published.map(item => <option key={item.id} value={item.id}>{item.name}</option>)}</select></label>
    <label>Inspector<select name="assigneeId" required><option value="">Choose an inspector</option>{data.inspectors.map(item => <option key={item.id} value={item.id}>{item.email}</option>)}</select></label>
    <label>Due date<input type="date" name="dueDate" required /></label>
    <div className="dialog-actions"><button className="button primary" disabled={busy}>Assign inspection</button></div>
  </form>;
}
