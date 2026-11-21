import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { ActivatedRoute, provideRouter } from '@angular/router';
import { of } from 'rxjs';
import { vi } from 'vitest';
import { TaskDetailComponent } from './task-detail.component';
import { TaskService } from '../../core/tasks/task.service';
import { ProjectService } from '../../core/projects/project.service';
import { TaskDetail } from '../../core/tasks/task.models';

describe('TaskDetailComponent', () => {
  let component: TaskDetailComponent;
  let fixture: ComponentFixture<TaskDetailComponent>;
  let taskService: TaskService;
  let projectService: ProjectService;

  const mockDetail: TaskDetail = {
    id: 'task-100',
    projectId: 'proj-1',
    projectCode: 'WFP',
    projectName: 'WorkFlowPro',
    title: 'Design API Specs',
    description: 'Detailed OpenAPI specs',
    status: 'ToDo',
    priority: 'High',
    assigneeId: 'user-1',
    assigneeName: 'Alex Developer',
    createdById: 'user-2',
    createdByName: 'Manager Mike',
    dueAtUtc: '2026-12-01T00:00:00Z',
    estimateMinutes: 90,
    version: 1,
    createdAtUtc: '2026-11-21T00:00:00Z',
    comments: [
      {
        id: 'c-1',
        taskId: 'task-100',
        authorId: 'user-2',
        authorName: 'Manager Mike',
        body: 'Please ensure RFC 7807 problem details are included.',
        createdAtUtc: '2026-11-21T01:00:00Z'
      }
    ],
    history: [
      {
        id: 'h-1',
        taskId: 'task-100',
        fromStatus: null,
        toStatus: 'ToDo',
        changedById: 'user-2',
        changedByName: 'Manager Mike',
        changedAtUtc: '2026-11-21T00:00:00Z',
        comment: 'Task created.'
      }
    ]
  };

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [TaskDetailComponent],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideRouter([]),
        {
          provide: ActivatedRoute,
          useValue: {
            snapshot: {
              paramMap: {
                get: (key: string) => key === 'id' ? 'task-100' : null
              }
            }
          }
        }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(TaskDetailComponent);
    component = fixture.componentInstance;
    taskService = TestBed.inject(TaskService);
    projectService = TestBed.inject(ProjectService);
  });

  it('loads task detail and renders attributes on init', () => {
    const getByIdSpy = vi.spyOn(taskService, 'getById').mockReturnValue(of(mockDetail));
    vi.spyOn(projectService, 'getMembers').mockReturnValue(of([]));

    fixture.detectChanges();

    expect(component).toBeTruthy();
    expect(getByIdSpy).toHaveBeenCalledWith('task-100');
    expect(component.task()?.title).toBe('Design API Specs');
    expect(component.task()?.status).toBe('ToDo');
    expect(component.task()?.comments.length).toBe(1);
    expect(component.task()?.history.length).toBe(1);
  });

  it('allows adding a new comment', () => {
    vi.spyOn(taskService, 'getById').mockReturnValue(of(mockDetail));
    vi.spyOn(projectService, 'getMembers').mockReturnValue(of([]));
    fixture.detectChanges();

    const addCommentSpy = vi.spyOn(taskService, 'addComment').mockReturnValue(of({
      id: 'c-2',
      taskId: 'task-100',
      authorId: 'user-1',
      authorName: 'Alex Developer',
      body: 'Will include problem details.',
      createdAtUtc: '2026-11-21T02:00:00Z'
    }));

    component.newCommentBody = 'Will include problem details.';
    component.onAddComment();

    expect(addCommentSpy).toHaveBeenCalledWith('task-100', { body: 'Will include problem details.' });
    expect(component.task()?.comments.length).toBe(2);
    expect(component.newCommentBody).toBe('');
  });
});
