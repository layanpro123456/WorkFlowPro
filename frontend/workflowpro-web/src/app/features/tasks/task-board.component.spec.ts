import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { provideRouter } from '@angular/router';
import { of } from 'rxjs';
import { vi } from 'vitest';
import { TaskBoardComponent } from './task-board.component';
import { TaskService } from '../../core/tasks/task.service';
import { ProjectService } from '../../core/projects/project.service';
import { GitService } from '../../core/git/git.service';
import { TaskSummary } from '../../core/tasks/task.models';

describe('TaskBoardComponent', () => {
  let component: TaskBoardComponent;
  let fixture: ComponentFixture<TaskBoardComponent>;
  let taskService: TaskService;
  let projectService: ProjectService;
  let gitService: GitService;

  const mockTasks: TaskSummary[] = [
    {
      id: 'task-1',
      projectId: 'proj-1',
      projectCode: 'CORE',
      projectName: 'Core Platform',
      taskNumber: 1,
      taskKey: 'CORE-1',
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
    gitService = TestBed.inject(GitService);
    vi.spyOn(gitService, 'listProjectRepositories').mockReturnValue(of([]));
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
    expect(component.getAllowedTransitions('InProgress')).toContain('InReview');
    expect(component.getAllowedTransitions('InReview')).toEqual(['Done', 'InProgress', 'Blocked', 'Cancelled']);
  });

  it('includes InReview column and creates branch from Kanban card', () => {
    expect(component.boardColumns).toContain('InReview');
    component.projectRepos.set([{
      id: 'repo-1',
      projectId: 'proj-1',
      provider: 'GitHub',
      repositoryOwner: 'octocat',
      repositoryName: 'core',
      repositoryFullName: 'octocat/core',
      apiBaseUrl: 'https://api.github.com',
      webBaseUrl: 'https://github.com',
      defaultBranch: 'main',
      hasAccessToken: true,
      hasWebhookSecret: true,
      webhookPath: '/api/v1/webhooks/git/github/repo-1',
      isDefault: true,
      isActive: true,
      autoLinkEnabled: true,
      autoTransitionEnabled: true,
      useInReviewColumn: true,
      createdAtUtc: '2026-11-28'
    }]);

    const createBranchSpy = vi.spyOn(gitService, 'createTaskBranch').mockReturnValue(of({
      id: 'br-1',
      taskId: 'task-1',
      repositoryId: 'repo-1',
      repositoryFullName: 'octocat/core',
      branchName: 'feat/CORE-1-task-1-in-backlog',
      baseBranch: 'main',
      branchUrl: 'https://github.com/octocat/core/tree/feat/CORE-1-task-1-in-backlog',
      createdAtUtc: '2026-11-28'
    }));
    vi.spyOn(taskService, 'listProjectTasks').mockReturnValue(of({
      items: mockTasks,
      page: 1,
      pageSize: 100,
      totalCount: 2,
      totalPages: 1,
      hasPreviousPage: false,
      hasNextPage: false
    }));

    component.selectedProjectId.set('proj-1');
    component.openCreateBranchModal(mockTasks[0]);
    expect(component.branchModel.branchName).toBe('feat/CORE-1-task-1-in-backlog');

    component.onCreateBranch();
    expect(createBranchSpy).toHaveBeenCalledWith('task-1', expect.objectContaining({
      repositoryId: 'repo-1',
      branchName: 'feat/CORE-1-task-1-in-backlog'
    }));
  });
});
