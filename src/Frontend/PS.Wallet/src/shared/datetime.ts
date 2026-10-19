// Formats an ISO-8601 UTC timestamp for display. For the MVP this is the browser's local format.
// A future iteration may switch to an explicit UTC display (see docs/api/api-guidelines.md §12).
export function formatDateTime(iso: string): string {
  const d = new Date(iso)
  if (Number.isNaN(d.getTime())) {
    return iso
  }
  return d.toLocaleString()
}
