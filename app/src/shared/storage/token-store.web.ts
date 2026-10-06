// SecureStore has no web implementation; localStorage survives a browser restart.
const KEY = 'timeforyou.token';

export async function getToken(): Promise<string | null> {
  return localStorage.getItem(KEY);
}

export async function setToken(token: string): Promise<void> {
  localStorage.setItem(KEY, token);
}

export async function clearToken(): Promise<void> {
  localStorage.removeItem(KEY);
}
