export function routeFromHash(hash) {
  const [path, query = ''] = hash.replace(/^#\/?/, '').split('?');
  const [page, ...parts] = path.split('/').filter(Boolean);
  return { page: page || 'forms', parts, query };
}

export function recordId(value) {
  if (!/^[1-9]\d*$/.test(value ?? '')) return null;
  const id = Number(value);
  return Number.isSafeInteger(id) ? id : null;
}

export function routeLink(page, ...parts) {
  return '#' + [page, ...parts.filter(part => part !== undefined)].join('/');
}
