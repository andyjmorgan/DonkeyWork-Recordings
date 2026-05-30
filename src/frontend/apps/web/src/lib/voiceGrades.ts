const GRADE_ORDER = ['A', 'A-', 'B+', 'B', 'B-', 'C+', 'C', 'C-', 'D+', 'D', 'D-', 'F+', 'F'];

// Lower rank = better grade. Ungraded voices sort last.
export function gradeRank(grade?: string): number {
  const i = grade ? GRADE_ORDER.indexOf(grade) : -1;
  return i < 0 ? 99 : i;
}
