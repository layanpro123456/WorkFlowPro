import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { GitService } from './git.service';

describe('GitService', () => {
  let service: GitService;
  let httpMock: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()]
    });
    service = TestBed.inject(GitService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpMock.verify();
  });

  it('lists project repositories', () => {
    service.listProjectRepositories('proj-1').subscribe(repos => {
      expect(repos.length).toBe(1);
      expect(repos[0].repositoryFullName).toBe('octocat/workflowpro');
    });

    const req = httpMock.expectOne('/api/v1/projects/proj-1/git/repositories');
    expect(req.request.method).toBe('GET');
    req.flush([
      {
        id: 'repo-1',
        projectId: 'proj-1',
        provider: 'GitHub',
        repositoryOwner: 'octocat',
        repositoryName: 'workflowpro',
        repositoryFullName: 'octocat/workflowpro',
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
        createdAtUtc: '2026-11-28T00:00:00Z'
      }
    ]);
  });

  it('creates a task branch', () => {
    service.createTaskBranch('task-1', { branchPrefix: 'feat' }).subscribe(branch => {
      expect(branch.branchName).toBe('feat/WFP-1-oauth');
    });

    const req = httpMock.expectOne('/api/v1/tasks/task-1/git/branches');
    expect(req.request.method).toBe('POST');
    req.flush({
      id: 'br-1',
      taskId: 'task-1',
      repositoryId: 'repo-1',
      repositoryFullName: 'octocat/workflowpro',
      branchName: 'feat/WFP-1-oauth',
      baseBranch: 'main',
      branchUrl: 'https://github.com/octocat/workflowpro/tree/feat/WFP-1-oauth',
      createdAtUtc: '2026-11-28T00:00:00Z'
    });
  });

  it('links and unlinks pull requests', () => {
    service.linkPullRequest('task-1', { repositoryId: 'repo-1', prNumber: 42 }).subscribe(pr => {
      expect(pr.prNumber).toBe(42);
    });

    const linkReq = httpMock.expectOne('/api/v1/tasks/task-1/git/pull-requests/link');
    expect(linkReq.request.method).toBe('POST');
    linkReq.flush({
      id: 'pr-1',
      repositoryId: 'repo-1',
      repositoryFullName: 'octocat/workflowpro',
      prNumber: 42,
      title: 'Add Git',
      state: 'Open',
      sourceBranch: 'feat/WFP-1-oauth',
      targetBranch: 'main',
      authorLogin: 'octocat',
      htmlUrl: 'https://github.com/octocat/workflowpro/pull/42',
      reviewStatus: 'Pending',
      checkStatus: 'Success',
      commitsCount: 1,
      additions: 10,
      deletions: 2,
      linkSource: 'Manual',
      openedAtUtc: '2026-11-28T00:00:00Z',
      lastSyncedAtUtc: '2026-11-28T00:00:00Z'
    });

    service.unlinkPullRequest('task-1', 'pr-1').subscribe();
    const unlinkReq = httpMock.expectOne('/api/v1/tasks/task-1/git/pull-requests/pr-1');
    expect(unlinkReq.request.method).toBe('DELETE');
    unlinkReq.flush(null);
  });
});
