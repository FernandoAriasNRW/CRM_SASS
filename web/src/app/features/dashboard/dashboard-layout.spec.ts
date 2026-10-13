import { clampToGrid, moveTo, overlaps, resizeTo, sameLayout, settle, type Placement } from './dashboard-layout';

const w = (id: string, x: number, y: number, width = 6, height = 4): Placement =>
  ({ id, reportId: `r-${id}`, x, y, width, height, visualization: 'bar', title: null });

const noOverlaps = (layout: Placement[]) =>
  layout.every((a, i) => layout.every((b, j) => i === j || !overlaps(a, b)));

describe('dashboard layout', () => {
  it('keeps a widget inside the twelve columns and the size limits the server accepts', () => {
    expect(clampToGrid(w('a', 10, -2, 6, 1))).toEqual(jasmine.objectContaining({ x: 6, y: 0, width: 6, height: 2 }));
    expect(clampToGrid(w('a', 0, 0, 1, 40))).toEqual(jasmine.objectContaining({ width: 3, height: 12 }));
  });

  it('moving onto another widget pushes it down instead of overlapping', () => {
    const layout = [w('a', 0, 0), w('b', 6, 0)];

    const result = moveTo(layout, 'b', 0, 0);

    expect(result.find(p => p.id === 'b')).toEqual(jasmine.objectContaining({ x: 0, y: 0 }));
    expect(result.find(p => p.id === 'a')).toEqual(jasmine.objectContaining({ x: 0, y: 4 }));
    expect(noOverlaps(result)).toBeTrue();
  });

  it('the others float up to fill the gap the moved one leaves', () => {
    const layout = [w('a', 0, 0), w('b', 0, 4), w('c', 0, 8)];

    const result = moveTo(layout, 'a', 6, 0);

    expect(result.find(p => p.id === 'b')!.y).toBe(0);
    expect(result.find(p => p.id === 'c')!.y).toBe(4);
  });

  it('dropping far below does not leave an empty band', () => {
    const result = moveTo([w('a', 0, 0), w('b', 6, 0)], 'b', 6, 30);

    expect(result.find(p => p.id === 'b')!.y).toBe(0);
  });

  it('growing a widget pushes the one below further down', () => {
    const layout = [w('a', 0, 0, 12, 4), w('b', 0, 4, 12, 4)];

    const result = resizeTo(layout, 'a', 12, 6);

    expect(result.find(p => p.id === 'b')!.y).toBe(6);
    expect(noOverlaps(result)).toBeTrue();
  });

  it('keeps the order the widgets came in and every field the server needs back', () => {
    const layout = [w('a', 0, 0), w('b', 6, 0), w('c', 0, 4)];

    const result = settle(moveTo(layout, 'c', 6, 0), 'c');

    expect(result.map(p => p.id)).toEqual(['a', 'b', 'c']);
    expect(result[1]).toEqual(jasmine.objectContaining({ reportId: 'r-b', visualization: 'bar' }));
  });

  it('knows when nothing changed, so nothing is saved', () => {
    const layout = [w('a', 0, 0), w('b', 6, 0)];

    expect(sameLayout(layout, moveTo(layout, 'a', 0, 0))).toBeTrue();
    expect(sameLayout(layout, moveTo(layout, 'a', 0, 5))).toBeTrue();
    expect(sameLayout(layout, moveTo(layout, 'b', 0, 0))).toBeFalse();
  });
});
