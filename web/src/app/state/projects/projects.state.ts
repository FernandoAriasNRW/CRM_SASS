import { createAction, createReducer, createSelector, on, props } from '@ngrx/store';

export interface Project {
  id: string;
  name: string;
  description: string;
  status: string;
  startDate: string;
  estimatedEndDate: string;
  ownerId: string;
  spaceId: string;
  folderId?: string;
}

export interface ProjectsState {
  items: Project[];
  loaded: boolean;
}

const initial: ProjectsState = { items: [], loaded: false };

export const projectsLoaded  = createAction('[Projects] Loaded',  props<{ items: Project[] }>());
export const projectCreated  = createAction('[Projects] Created', props<{ item: Project }>());
export const projectUpdated  = createAction('[Projects] Updated', props<{ item: Project }>());
export const projectDeleted  = createAction('[Projects] Deleted', props<{ id: string }>());

export const projectsReducer = createReducer(
  initial,
  on(projectsLoaded,  (s, { items }) => ({ items, loaded: true })),
  on(projectCreated,  (s, { item })  => ({ ...s, items: [...s.items, item] })),
  on(projectUpdated,  (s, { item })  => ({
    ...s,
    items: s.items.map(p => p.id === item.id ? item : p),
  })),
  on(projectDeleted,  (s, { id })    => ({
    ...s,
    items: s.items.filter(p => p.id !== id),
  })),
);

const selectFeature  = (state: any) => state.projects as ProjectsState;
export const selectProjects     = createSelector(selectFeature, s => s.items);
export const selectProjectsLoaded = createSelector(selectFeature, s => s.loaded);
