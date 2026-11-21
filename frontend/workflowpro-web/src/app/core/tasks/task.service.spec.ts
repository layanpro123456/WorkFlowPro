import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TaskService } from './task.service';
import { PagedResult } from '../projects/project.models';
import { TaskDetail, TaskSummary } from './task.models';

describe('TaskService', () => {
  let service: TaskService;
  let httpTesting: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting()
      ]
    });
    service = TestBed.inject(TaskService);
    httpTesting = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpTesting.verify();
  });

  it('listProjectTasks sends correct query parameters', () => {
    const mockResult: PagedResult<TaskSummary> = {
      items: [{
        id: 'task-1',
        projectId: 'proj-1',
        projectCode: 'CORE',
        projectName: 'Core Platform',
        title: 'Task 1',
        status: 'Backlog',
        priority: 'High',
        createdById: 'user-1',
        createdByName: 'Admin',
        version: 1,
        commentCount: 0,
        createdAtUtc: '2026-11-21'
      }],
      page: 1,
      pageSize: 50,
      totalCount: 1,
      totalPages: 1,
      hasPreviousPage: false,
      hasNextPage: false
    };

    service.listProjectTasks('proj-1', { status: 'Backlog', priority: 'High', search: 'Task', page: 1, pageSize: 50 }).subscribe(res => {
      expect(res).toEqual(mockResult);
    });

    const req = httpTesting.expectOne(r =>
      r.url === '/api/v1/projects/proj-1/tasks' &&
      r.params.get('status') === 'Backlog' &&
      r.params.get('priority') === 'High' &&
      r.params.get('search') === 'Task' &&
      r.params.get('page') === '1' &&
      r.params.get('pageSize') === '50'
    );
    expect(req.request.method).toBe('GET');
    req.flush(mockResult);
  });

  it('getById fetches task detail', () => {
    const mockDetail: TaskDetail = {
      id: 'task-123',
      projectId: 'proj-1',
      projectCode: 'CORE',
      projectName: 'Core Platform',
      title: 'Detailed Task',
      status: 'InProgress',
      priority: 'Normal',
      createdById: 'user-1',
      createdByName: 'Admin',
      version: 2,
      createdAtUtc: '2026-11-21',
      comments: [],
      history: []
    };

    service.getById('task-123').subscribe(res => {
      expect(res).toEqual(mockDetail);
    });

    const req = httpTesting.expectOne('/api/v1/tasks/task-123');
    expect(req.request.method).toBe('GET');
    req.flush(mockDetail);
  });

  it('changeStatus patches status with version', () => {
    const mockUpdated: TaskSummary = {
      id: 'task-123',
      projectId: 'proj-1',
      projectCode: 'CORE',
      projectName: 'Core',
      title: 'Detailed Task',
      status: 'Done',
      priority: 'Normal',
      createdById: 'user-1',
      createdByName: 'Admin',
      version: 3,
      commentCount: 0,
      createdAtUtc: '2026-11-21'
    };

    service.changeStatus('task-123', { status: 'Done', version: 2, comment: 'Completed work' }).subscribe(res => {
      expect(res).toEqual(mockUpdated);
    });

    const req = httpTesting.expectOne('/api/v1/tasks/task-123/status');
    expect(req.request.method).toBe('PATCH');
    expect(req.request.body).toEqual({ status: 'Done', version: 2, comment: 'Completed work' });
    req.flush(mockUpdated);
  });

  it('addComment posts comment body', () => {
    const mockComment = {
      id: 'c-1',
      taskId: 'task-123',
      authorId: 'u-1',
      authorName: 'Admin',
      body: 'Looking good',
      createdAtUtc: '2026-11-21'
    };

    service.addComment('task-123', { body: 'Looking good' }).subscribe(res => {
      expect(res).toEqual(mockComment);
    });

    const req = httpTesting.expectOne('/api/v1/tasks/task-123/comments');
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual({ body: 'Looking good' });
    req.flush(mockComment);
  });
});
