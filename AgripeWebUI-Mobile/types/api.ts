export interface QuadranteData {
  topLeft: string;
  topLeftAvg?: number;
  topRight: string;
  topRightAvg?: number;
  bottomLeft: string;
  bottomLeftAvg?: number;
  bottomRight: string;
  bottomRightAvg?: number;
}

export interface Pivot {
  id: number;
  name: string;
  quadrante?: QuadranteData;
}

export interface Sensor {
  id: number;
  name: string;
  pivot: Pivot;
  quadrante: number;
  code: string;
}

export interface ReadEntry {
  id: number;
  sensorId: number;
  value: number;
  date: string;
  /** Local-only: queued offline and not yet synced */
  pendingSync?: boolean;
  /** Local queue item id for pending reads */
  localQueueId?: string;
}

export interface CreateManualReadRequest {
  code: string;
  value: number;
}

export interface CreateManualReadResponse {
  id: number;
  sensorId: number;
  userId: number;
  value: number;
}

export interface User {
  id: number;
  name: string;
  email: string;
  active: boolean;
}

export interface LoginRequest {
  email: string;
  password: string;
}

export interface LoginResponse {
  token: string;
}

export interface ExternalLoginRequest {
  provider: string;
  code: string;
  redirectUri: string;
}

export interface CreateUserRequest {
  name: string;
  email: string;
  password: string;
}

export interface EditUserRequest {
  name: string;
  email: string;
  password?: string;
}

export interface CreatePivotRequest {
  name: string;
}

export interface EditPivotRequest {
  id: number;
  name: string;
}

export interface CreateSensorRequest {
  name: string;
  code: string;
  quadrante: number;
  pivot: { id: number; name: string };
}

export interface EditSensorRequest {
  id: number;
  name: string;
  code: string;
  quadrante: number;
  pivot: { id: number; name: string };
}

export interface GetReadByPivotIdResponse {
  id?: number;
  name?: string;
  quadrante?: QuadranteData;
}
