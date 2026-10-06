// Hand-written DTO mirrors of the backend contract.

/** Access level on a resource (Workspace/Folder/Link). Effective = highest of admin/ownership/share. */
export type AccessLevel = "Viewer" | "Editor" | "Manager";

export type ResourceType = "Workspace" | "Folder" | "Link";

export interface Me {
  oid: string;
  email: string;
  name: string;
  isGlobalAdmin: boolean;
}

export interface WorkspaceSummary {
  id: string;
  name: string;
  isPersonal: boolean;
  access: AccessLevel;
  isOwner: boolean;
  linkCount: number;
  ownerName: string | null;
  ownerEmail: string | null;
}

export interface WorkspaceDetail {
  id: string;
  name: string;
  isPersonal: boolean;
  access: AccessLevel;
  isOwner: boolean;
  createdOnUtc: string;
  folderCount: number;
  linkCount: number;
  ownerName: string | null;
  ownerEmail: string | null;
}

export interface AdminLink {
  id: string;
  workspaceId: string;
  workspaceName: string;
  ownerName: string | null;
  code: string;
  shortUrl: string;
  destination: string;
  title: string | null;
  isActive: boolean;
  clickCount: number;
  createdOnUtc: string;
}

export interface AdminWorkspace {
  id: string;
  name: string;
  isPersonal: boolean;
  ownerName: string | null;
  ownerEmail: string | null;
  folderCount: number;
  linkCount: number;
  createdOnUtc: string;
}

export interface DeletedLink {
  id: string;
  workspaceName: string;
  ownerName: string | null;
  code: string;
  shortUrl: string;
  destination: string;
  title: string | null;
  clickCount: number;
  deletedOnUtc: string;
  deletedByName: string | null;
}

export interface Folder {
  id: string;
  workspaceId: string;
  name: string;
  createdOnUtc: string;
  linkCount: number;
  access: AccessLevel;
  isOwner: boolean;
}

export interface Link {
  id: string;
  workspaceId: string;
  folderId: string | null;
  code: string;
  shortUrl: string;
  destination: string;
  title: string | null;
  notes: string | null;
  tags: string[];
  isActive: boolean;
  expiresOnUtc: string | null;
  clickCount: number;
  createdOnUtc: string;
  updatedOnUtc: string | null;
}

export interface CreateLinkBody {
  destination: string;
  code?: string | null;
  folderId?: string | null;
  title?: string | null;
  notes?: string | null;
  tags?: string[];
  expiresOnUtc?: string | null;
}

export type UpdateLinkBody = Omit<CreateLinkBody, "code">;

export interface RenameLinkBody {
  code: string;
}

export type LinkEventType =
  | "Created"
  | "DestinationChanged"
  | "FolderMoved"
  | "MetadataChanged"
  | "Enabled"
  | "Disabled"
  | "ExpiryChanged"
  | "Renamed";

export interface LinkEvent {
  type: LinkEventType;
  actorName: string;
  oldValue: string | null;
  newValue: string | null;
  createdOnUtc: string;
}

// ---- Sharing ----
export interface Share {
  id: string;
  resourceType: ResourceType;
  resourceId: string;
  granteeOid: string;
  granteeEmail: string;
  granteeName: string;
  level: AccessLevel;
  createdOnUtc: string;
}

export interface CreateShareBody {
  resourceType: ResourceType;
  resourceId: string;
  email: string;
  oid?: string | null;
  name?: string | null;
  level: AccessLevel;
}

export interface SharedWithMe {
  resourceType: ResourceType;
  resourceId: string;
  workspaceId: string | null;
  name: string;
  detail: string | null;
  level: AccessLevel;
}

// ---- Branding ----
export interface Branding {
  appName: string;
  tagline: string | null;
  homeUrl: string | null;
  primaryColor: string | null;
  hasLogo: boolean;
  logoUrl: string | null;
  shortBaseUrl: string;
  shareEmailDomain: string | null;
  version: number;
}

export interface UpdateBrandingBody {
  appName: string;
  tagline?: string | null;
  homeUrl?: string | null;
  primaryColor?: string | null;
}

// ---- Analytics ----
export interface CountByKey {
  key: string;
  count: number;
}

export interface DayCount {
  day: string;
  count: number;
}

export interface TopLink {
  linkId: string;
  code: string;
  destination: string;
  clicks: number;
}

export interface WorkspaceAnalytics {
  workspaceId: string;
  totalClicks: number;
  totalLinks: number;
  activeLinks: number;
  windowDays: number;
  topLinks: TopLink[];
  clicksByDay: DayCount[];
}

export interface LinkAnalytics {
  linkId: string;
  code: string;
  totalClicks: number;
  uniqueVisitors: number;
  windowDays: number;
  clicksByDay: DayCount[];
  topReferrers: CountByKey[];
  byBrowser: CountByKey[];
  byOs: CountByKey[];
  byDevice: CountByKey[];
  byCountry: CountByKey[];
}
