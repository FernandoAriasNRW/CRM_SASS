import { Injectable } from '@angular/core';
import { ColumnDef } from '../ui/data-table/data-table.component';

export type ColumnConfig<T> = {
  [K in keyof T]?: Partial<ColumnDef>;
};

@Injectable({
  providedIn: 'root'
})
export class TableColumnService {
  
  /**
   * Generates a ColumnDef array from a configuration object bound to a type T.
   * This validates that column keys match the properties of the model.
   * 
   * @param config A configuration object where keys are properties of T.
   * @param extraColumns Any additional columns not present in the model (e.g. 'actions')
   */
  buildColumns<T>(
    config: ColumnConfig<T>,
    extraColumns: ColumnDef[] = []
  ): ColumnDef[] {
    const columns: ColumnDef[] = [];
    
    for (const key in config) {
      if (Object.prototype.hasOwnProperty.call(config, key)) {
        const def = config[key];
        if (def) {
          columns.push({
            key: key as string,
            label: def.label || this.formatLabel(key as string),
            sortable: def.sortable ?? true,
            visible: def.visible ?? true,
            type: def.type ?? 'text',
            template: def.template
          });
        }
      }
    }
    
    return [...columns, ...extraColumns];
  }

  private formatLabel(key: string): string {
    // Convert camelCase to Title Case (e.g., 'estimatedHours' -> 'Estimated Hours')
    const result = key.replace(/([A-Z])/g, ' $1');
    return result.charAt(0).toUpperCase() + result.slice(1);
  }
}
