import { create } from "zustand";

const STORAGE_KEY = "beacon.selectedWorkspace";

function load(): string | null {
  try {
    return localStorage.getItem(STORAGE_KEY);
  } catch {
    return null;
  }
}

function save(id: string | null): void {
  try {
    if (id) {
      localStorage.setItem(STORAGE_KEY, id);
    } else {
      localStorage.removeItem(STORAGE_KEY);
    }
  } catch {
    // ignore — private mode / blocked storage
  }
}

interface WorkspaceState {
  selectedWorkspaceId: string | null;
  selectedFolderId: string | null;
  setWorkspace: (id: string) => void;
  setFolder: (id: string | null) => void;
}

export const useWorkspaceStore = create<WorkspaceState>((set) => ({
  selectedWorkspaceId: load(),
  selectedFolderId: null,
  setWorkspace: (id) => {
    save(id);
    set({ selectedWorkspaceId: id, selectedFolderId: null }); // reset folder filter on switch
  },
  setFolder: (id) => set({ selectedFolderId: id }),
}));
