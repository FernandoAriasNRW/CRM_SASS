import { createAction, createReducer, on, props } from '@ngrx/store';

export interface AuthState {
  token: string | null;
  isAuthenticated: boolean;
}

export const initialAuthState: AuthState = {
  token: null,
  isAuthenticated: false
};

export const authLoggedIn = createAction('[Auth] Logged In', props<{ token: string }>());
export const authLoggedOut = createAction('[Auth] Logged Out');

export const authReducer = createReducer(
  initialAuthState,
  on(authLoggedIn, (state, { token }) => ({ ...state, token, isAuthenticated: true })),
  on(authLoggedOut, () => ({ token: null, isAuthenticated: false }))
);
