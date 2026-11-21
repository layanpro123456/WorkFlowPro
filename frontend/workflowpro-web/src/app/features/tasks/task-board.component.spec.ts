import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { provideRouter } from '@angular/router';
import { of } from 'rxjs';
import { vi } from 'vitest';
import { TaskBoardComponent } from './task-board.component';
import { TaskService } from '../../core/tasks/task.service';
import { ProjectService } from '../../core/projects/project.service';
import { TaskSummary } from '../../core/tasks/task.models';

describe('TaskBoardComponent', () => {
  let component: TaskBoardComponent;
  let fixture: ComponentFixture<TaskBoardComponent>;
  let taskService: TaskService;
  let projectService: ProjectService;

  const mockTasks: TaskSummary[] = [
    {
      id: 'task-1',
      projectId: 'proj-1',
      projectCode: 'CORE',
      projectName: 'Core Platform',
      title: 'Task 1 in Backlog',
      status: 'Backlog',
      priority: 'Normal',
      createdById: 'u-1',
      createdByName: 'Admin',
      version: 1,
      commentCount: 0,
      createdAtUtc: '2026-11-21'
    },
    {
      id: 'task-2',
      projectId: 'proj-1',
      projectCode: 'CORE',
      projectName: 'Core Platform',
      title: 'Task 2 in ToDo',
      status: 'ToDo',
      priority: 'Urgent',
      createdById: 'u-1',
      createdByName: 'Admin',
      version: 1,
      commentCount: 1,
      createdAtUtc: '2026-11-21'
    }
  ];

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [TaskBoardComponent],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideRouter([])
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(TaskBoardComponent);
    component = fixture.componentInstance;
    taskService = TestBed.inject(TaskService);
    projectService = TestBed.inject(ProjectService);
  });

  it('loads projects and project tasks on init', () => {
    vi.spyOn(projectService, 'list').mockReturnValue(of({
      items: [{
        id: 'proj-1',
        projectCode: 'CORE',
        name: 'Core Platform',
        status: 'Active',
        memberCount: 2,
        createdAtUtc: '2026-11-21'
      }],
      page: 1,
      pageSize: 50,
      totalCount: 1,
      totalPages: 1,
      hasPreviousPage: false,
      hasNextPage: false
    }));

    vi.spyOn(projectService, 'getMembers').mockReturnValue(of([]));

    const listSpy = vi.spyOn(taskService, 'listProjectTasks').mockReturnValue(of({
      items: mockTasks,
      page: 1,
      pageSize: 50,
      totalCount: 2,
      totalPages: 1,
      hasPreviousPage: false,
      hasNextPage: false
    }));

    fixture.detectChanges();

    expect(component).toBeTruthy();
    expect(listSpy).toHaveBeenCalledWith('proj-1', expect.anything());
    expect(component.tasks().length).toBe(2);
    expect(component.getTasksForStatus('Backlog').length).toBe(1);
    expect(component.getTasksForStatus('ToDo').length).toBe(1);
  });

  it('switches view between board and list', () => {
    expect(component.activeView()).toBe('board');
    component.activeView.set('list');
    expect(component.activeView()).toBe('list');
  });

  it('getAllowedTransitions returns correct target statuses', () => {
    expect(component.getAllowedTransitions('Backlog')).toEqual(['ToDo', 'Cancelled']);
    expect(component.getAllowedTransitions('ToDo')).toEqual(['InProgress', 'Backlog', 'Cancelled']);
  });
});
