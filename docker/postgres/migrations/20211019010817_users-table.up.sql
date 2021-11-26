create table users (
    id bigint generated always as identity,
    user_twitch_id character varying(30) not null unique,
    login_name character varying(30) not null,
    display_name character varying(30) not null,
    balance bigint not null default 0,
    last_updated timestamp without time zone not null,
    primary key(id)
);

ALTER TABLE users OWNER TO drakoapi;

create index ix_users__balance on users (
    balance DESC, last_updated DESC
)
include (user_twitch_id, display_name, login_name);

create table transactions (
    id bigint generated always as identity,
    user_id bigint not null,
    date timestamp without time zone not null,
    amount bigint not null,
    balance bigint not null,
    reason character varying(300) not null,
    unique_id text null unique,
    constraint fk_user foreign key(user_id) references users(id)
);

alter table transactions owner to drakoapi;