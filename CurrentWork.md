# Current Work

## Origin

This solution was created while working on `Tripous.Avalon`.

In `Tripous.Avalon` we designed a new clean locator stack (`LocatorDef2`, `Locator2`, `Locators2`, `LocatorResult2`, `LocatorMapper2`) but deliberately stopped before adapting it to the existing Avalonia `DataGrid`.

The reason is that the current Avalonia `DataGrid` does not fit the desired Tripous desktop data-entry workflow well enough. Before adapting locators and lookup editing, we want a better grid foundation.

## Goal

Build a reusable Avalonia controls library, independent from Tripous.

The first major control is `GroupGrid`.

The library must be usable by any Avalonia developer without referencing or understanding Tripous.

## Boundaries

- No reference to `Tripous`.
- No reference to `Tripous.Data`.
- No `MemTable`, `TableDef`, `FieldDef`, `LocatorDef2`, `Locator2`, `LookupDef`, or Tripous registry concepts.
- No Tripous naming conventions in the core API.
- No Tripous adapters inside this solution.
- Any future Tripous integration must happen from the Tripous side, by adapting to the public API of this library.

## Naming Direction

- Solution/repo: likely `Avalonia.Controls.Extras`.
- Main library: `Avalonia.Controls.Extras`.
- Control namespace: `Avalonia.Controls`.
- First control: `GroupGrid`.
- Tests: `Avalonia.Controls.Extras.Tests`.
- Demo app: `Avalonia.Controls.Extras.Demo`.

## GroupGrid Direction
`GroupGrid` should be a general-purpose Avalonia grid/control inspired partly by the design experience from Tripous JavaScript `tp.Grid`, but implemented natively for Avalonia.

It should support data-entry and business application workflows better than the stock grid.

Initial design areas:

- Row/item abstraction.
- Column descriptors.
- Cell value get/set.
- Selection model.
- Current cell and focused cell.
- Keyboard navigation.
- Editing lifecycle.
- Cell editors.
- Validation hooks.
- Command hooks.
- Sorting/filtering/grouping later, not necessarily first.
- Stable layout and predictable behavior for dense data-entry screens.

## Tripous Future Integration

Later, in `Tripous.Avalon`, Tripous-specific code may adapt `GroupGrid` to:

- `MemTable` / `DataView`.
- `TableDef` / `FieldDef`.
- Captions and formatting.
- Validation.
- Lookup editors.
- Locator editors based on `Locator2`.
- Detail-grid toolbar behavior.

That integration belongs outside this solution.

## Immediate Next Step

Start with design, not implementation.

First define:

- What `GroupGrid` owns.
- What the data adapter contract looks like.
- What a column descriptor contains.
- How selection/current cell/editing should work.
- What the smallest useful demo should prove.
 
