import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { api, V1 } from "@/lib/api";
import type {
  AccessLevel,
  AdminLink,
  AdminWorkspace,
  Branding,
  DeletedLink,
  CreateLinkBody,
  CreateShareBody,
  Folder,
  LinkAnalytics,
  Link,
  LinkEvent,
  Me,
  ResourceType,
  Share,
  SharedWithMe,
  UpdateBrandingBody,
  UpdateLinkBody,
  WorkspaceAnalytics,
  WorkspaceDetail,
  WorkspaceSummary,
} from "@/lib/types";

const ws = (id: string) => `${V1}/workspaces/${id}`;

// ---- Identity ----
export const useMe = () => useQuery({ queryKey: ["me"], queryFn: () => api.get<Me>(`${V1}/me`) });

// ---- Branding (public read; super-admin writes) ----
export const useBranding = () =>
  useQuery({ queryKey: ["branding"], queryFn: () => api.getRoot<Branding>("/branding"), staleTime: 60_000 });

export function useUpdateBranding() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (body: UpdateBrandingBody) => api.put<Branding>(`${V1}/branding`, body),
    onSuccess: () => void qc.invalidateQueries({ queryKey: ["branding"] }),
  });
}

export function useUploadLogo() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (file: File) => {
      const fd = new FormData();
      fd.append("file", file);
      return api.postForm<Branding>(`${V1}/branding/logo`, fd);
    },
    onSuccess: () => void qc.invalidateQueries({ queryKey: ["branding"] }),
  });
}

export function useDeleteLogo() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: () => api.del<void>(`${V1}/branding/logo`),
    onSuccess: () => void qc.invalidateQueries({ queryKey: ["branding"] }),
  });
}

// ---- Workspaces ----
export const useWorkspaces = () =>
  useQuery({ queryKey: ["workspaces"], queryFn: () => api.get<WorkspaceSummary[]>(`${V1}/workspaces`) });

/** Single workspace by id — used to resolve a workspace a GA drilled into that isn't in their own list. */
export const useWorkspace = (id: string | undefined) =>
  useQuery({
    queryKey: ["workspace", id],
    queryFn: () => api.get<WorkspaceDetail>(`${ws(id!)}`),
    enabled: !!id,
  });

export function useCreateWorkspace() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (name: string) => api.post<WorkspaceSummary>(`${V1}/workspaces`, { name }),
    onSuccess: () => {
      void qc.invalidateQueries({ queryKey: ["workspaces"] });
      void qc.invalidateQueries({ queryKey: ["me"] });
    },
  });
}

export function useRenameWorkspace() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (v: { id: string; name: string }) => api.put<void>(`${V1}/workspaces/${v.id}`, { name: v.name }),
    onSuccess: () => void qc.invalidateQueries({ queryKey: ["workspaces"] }),
  });
}

export function useDeleteWorkspace() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (id: string) => api.del<void>(`${V1}/workspaces/${id}`),
    onSuccess: () => void qc.invalidateQueries({ queryKey: ["workspaces"] }),
  });
}

// ---- Folders ----
export const useFolders = (workspaceId: string) =>
  useQuery({
    queryKey: ["folders", workspaceId],
    queryFn: () => api.get<Folder[]>(`${ws(workspaceId)}/folders`),
    enabled: !!workspaceId,
  });

export function useCreateFolder(workspaceId: string) {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (name: string) => api.post<Folder>(`${ws(workspaceId)}/folders`, { name }),
    onSuccess: () => void qc.invalidateQueries({ queryKey: ["folders", workspaceId] }),
  });
}

export function useRenameFolder(workspaceId: string) {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (v: { id: string; name: string }) => api.put<void>(`${ws(workspaceId)}/folders/${v.id}`, { name: v.name }),
    onSuccess: () => void qc.invalidateQueries({ queryKey: ["folders", workspaceId] }),
  });
}

export function useDeleteFolder(workspaceId: string) {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (id: string) => api.del<void>(`${ws(workspaceId)}/folders/${id}`),
    onSuccess: () => {
      void qc.invalidateQueries({ queryKey: ["folders", workspaceId] });
      void qc.invalidateQueries({ queryKey: ["links", workspaceId] });
    },
  });
}

// ---- Links ----
export function useLinks(workspaceId: string, folderId: string | null, search: string) {
  const params = new URLSearchParams();
  if (folderId) params.set("folderId", folderId);
  if (search) params.set("search", search);
  const qs = params.toString();
  return useQuery({
    queryKey: ["links", workspaceId, folderId, search],
    queryFn: () => api.get<Link[]>(`${ws(workspaceId)}/links${qs ? `?${qs}` : ""}`),
    enabled: !!workspaceId,
  });
}

function invalidateLinks(qc: ReturnType<typeof useQueryClient>, workspaceId: string) {
  void qc.invalidateQueries({ queryKey: ["links", workspaceId] });
  void qc.invalidateQueries({ queryKey: ["folders", workspaceId] });
}

export function useCreateLink(workspaceId: string) {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (body: CreateLinkBody) => api.post<Link>(`${ws(workspaceId)}/links`, body),
    onSuccess: () => invalidateLinks(qc, workspaceId),
  });
}

export function useUpdateLink(workspaceId: string) {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (v: { id: string; body: UpdateLinkBody }) => api.put<Link>(`${ws(workspaceId)}/links/${v.id}`, v.body),
    onSuccess: () => invalidateLinks(qc, workspaceId),
  });
}

export function useRenameLink(workspaceId: string) {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (v: { id: string; code: string }) => api.post<Link>(`${ws(workspaceId)}/links/${v.id}/rename`, { code: v.code }),
    onSuccess: () => invalidateLinks(qc, workspaceId),
  });
}

export function useSetLinkActive(workspaceId: string) {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (v: { id: string; isActive: boolean }) =>
      api.patch<void>(`${ws(workspaceId)}/links/${v.id}/active`, { isActive: v.isActive }),
    onSuccess: () => invalidateLinks(qc, workspaceId),
  });
}

export function useDeleteLink(workspaceId: string) {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (id: string) => api.del<void>(`${ws(workspaceId)}/links/${id}`),
    onSuccess: () => invalidateLinks(qc, workspaceId),
  });
}

export const useLinkEvents = (workspaceId: string, linkId: string, enabled: boolean) =>
  useQuery({
    queryKey: ["link-events", workspaceId, linkId],
    queryFn: () => api.get<LinkEvent[]>(`${ws(workspaceId)}/links/${linkId}/events`),
    enabled: enabled && !!workspaceId && !!linkId,
  });

// ---- Admin (Global Admin only) ----
export const useAdminLinks = (search: string, enabled: boolean) =>
  useQuery({
    queryKey: ["admin-links", search],
    queryFn: () => api.get<AdminLink[]>(`${V1}/admin/links${search ? `?search=${encodeURIComponent(search)}` : ""}`),
    enabled,
  });

export const useAdminWorkspaces = (search: string, enabled: boolean) =>
  useQuery({
    queryKey: ["admin-workspaces", search],
    queryFn: () => api.get<AdminWorkspace[]>(`${V1}/admin/workspaces${search ? `?search=${encodeURIComponent(search)}` : ""}`),
    enabled,
  });

export const useDeletedLinks = (enabled: boolean) =>
  useQuery({
    queryKey: ["admin-deleted-links"],
    queryFn: () => api.get<DeletedLink[]>(`${V1}/admin/links/deleted`),
    enabled,
  });

function invalidateAllLinkViews(qc: ReturnType<typeof useQueryClient>) {
  void qc.invalidateQueries({ queryKey: ["admin-deleted-links"] });
  void qc.invalidateQueries({ queryKey: ["admin-links"] });
  void qc.invalidateQueries({ queryKey: ["links"] });
  void qc.invalidateQueries({ queryKey: ["folders"] });
}

export function useRestoreLink() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (id: string) => api.post<void>(`${V1}/admin/links/${id}/restore`, {}),
    onSuccess: () => invalidateAllLinkViews(qc),
  });
}

export function usePurgeLink() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (id: string) => api.del<void>(`${V1}/admin/links/${id}`),
    onSuccess: () => void qc.invalidateQueries({ queryKey: ["admin-deleted-links"] }),
  });
}

// ---- Sharing ----
const shareKey = (resourceType: ResourceType, resourceId: string) => ["shares", resourceType, resourceId];

export const useShares = (resourceType: ResourceType, resourceId: string) =>
  useQuery({
    queryKey: shareKey(resourceType, resourceId),
    queryFn: () => api.get<Share[]>(`${V1}/shares?resourceType=${resourceType}&resourceId=${resourceId}`),
    enabled: !!resourceId,
  });

export function useCreateShare() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (body: CreateShareBody) => api.post<Share>(`${V1}/shares`, body),
    onSuccess: (_data, body) => void qc.invalidateQueries({ queryKey: shareKey(body.resourceType, body.resourceId) }),
  });
}

export function useChangeShare(resourceType: ResourceType, resourceId: string) {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (v: { grantId: string; level: AccessLevel }) => api.put<void>(`${V1}/shares/${v.grantId}`, { level: v.level }),
    onSuccess: () => void qc.invalidateQueries({ queryKey: shareKey(resourceType, resourceId) }),
  });
}

export function useDeleteShare(resourceType: ResourceType, resourceId: string) {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (grantId: string) => api.del<void>(`${V1}/shares/${grantId}`),
    onSuccess: () => void qc.invalidateQueries({ queryKey: shareKey(resourceType, resourceId) }),
  });
}

export const useSharedWithMe = () =>
  useQuery({ queryKey: ["shares", "with-me"], queryFn: () => api.get<SharedWithMe[]>(`${V1}/shares/with-me`) });

// ---- Analytics ----
export const useWorkspaceAnalytics = (workspaceId: string, days: number) =>
  useQuery({
    queryKey: ["analytics", "workspace", workspaceId, days],
    queryFn: () => api.get<WorkspaceAnalytics>(`${ws(workspaceId)}/analytics?days=${days}`),
    enabled: !!workspaceId,
  });

export const useLinkAnalytics = (workspaceId: string, linkId: string, days: number) =>
  useQuery({
    queryKey: ["analytics", "link", workspaceId, linkId, days],
    queryFn: () => api.get<LinkAnalytics>(`${ws(workspaceId)}/links/${linkId}/analytics?days=${days}`),
    enabled: !!workspaceId && !!linkId,
  });
